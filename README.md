# TTSAlbion

> **Text-to-Speech bridge for Albion Online** — escucha el chat del juego en tiempo real y vocaliza los mensajes del jugador registrado a través de Discord Bot, micrófono virtual o altavoz local.

---

## ¿Qué hace?

TTSAlbion es una aplicación de escritorio para Windows que:

1. **Captura paquetes de red** del cliente de Albion Online en tiempo real (escucha pasiva, sin modificar el juego).
2. **Parsea eventos Photon** (el protocolo de red de Albion) para extraer mensajes de chat (`ChatMessage` y `ChatSay`).
3. **Filtra por jugador registrado** — solo procesa mensajes del usuario configurado.
4. **Detecta un prefijo de comando** configurable (por defecto `!!`) en el texto del mensaje.
5. **Sintetiza voz** con Windows SAPI (Text-to-Speech nativo) a partir del texto detectado.
6. **Envía el audio** al destino seleccionado: altavoz local, micrófono virtual (VB-Audio Cable) o un bot de Discord en un canal de voz.

### Caso de uso principal

Un jugador quiere que su equipo escuche en Discord lo que él escribe en el chat del juego, sin necesidad de usar el micrófono. Escribe `!! atacar base norte` en el chat de Albion y el bot de Discord lo vocaliza en el canal de voz automáticamente.

---

## Requisitos previos

### Software obligatorio

| Dependencia | Motivo | Descarga |
|---|---|---|
| **.NET 9 / .NET 10 Runtime** | Runtime de la aplicación | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| **VB-Audio Virtual Cable** | Micrófono virtual para el sink `VirtualMic` | [vb-audio.com/Cable](https://vb-audio.com/Cable/) |
| **Albion Online** (cliente) | La app escucha sus paquetes de red | [albiononline.com](https://albiononline.com) |

### DLLs nativas (incluidas en el build)

| Archivo | Versión requerida |
|---|---|
| `opus.dll` (x64) | ≥ 1.3 |
| `libsodium.dll` (x64) | ≥ 1.0.18 |

> ⚠️ Ambas DLLs deben estar en el mismo directorio que el ejecutable. La aplicación valida su presencia al arrancar y falla de forma explícita si no las encuentra (`NativeDependencyGuard`).

### Instalación de VB-Audio Virtual Cable

1. Descarga e instala **VB-Cable Driver Pack** desde [vb-audio.com/Cable](https://vb-audio.com/Cable/).
2. Reinicia Windows si se solicita.
3. En Discord → Ajustes de voz → selecciona **CABLE Output** como dispositivo de entrada.
4. En TTSAlbion, selecciona el sink `VirtualMic` — el audio TTS llegará a Discord como si fuera tu micrófono.

### Permisos de administrador

La aplicación requiere **ejecutarse como Administrador** (declarado en `app.manifest`) porque usa raw sockets para capturar paquetes IP a nivel de adaptador de red.

---

## Instalación y configuración

```
1. Descarga el build (zip) y extrae en cualquier carpeta.
2. Asegúrate de que opus.dll y libsodium.dll están en la misma carpeta.
3. Instala VB-Audio Virtual Cable (ver sección anterior).
4. Ejecuta TTSAlbion.exe como Administrador.
5. Configura el jugador, prefijo y salida de audio en la UI.
```

El archivo `Datos/config.json` se genera automáticamente al aplicar cualquier configuración y persiste entre sesiones.

---

## Configuración en la UI

| Campo | Descripción |
|---|---|
| **Jugador** | Nombre exacto del personaje en Albion (sensible a mayúsculas de forma flexible — comparación `OrdinalIgnoreCase`). |
| **Fuente de mensajes** | `ChatMessage` (zona/global) y/o `ChatSay` (/say en mundo). Se pueden activar ambos. |
| **Prefijo de comando** | Texto que debe iniciar el mensaje para activar TTS. Por defecto `!!`. |
| **Salida de audio** | `Local` (altavoz), `VirtualMic` (CABLE Input → Discord micrófono), `DiscordBot` (bot en canal de voz). |
| **Token / Guild ID / Channel ID** | Solo necesarios para el sink `DiscordBot`. |

---

## Arquitectura

### Vista de alto nivel

```
┌─────────────────────────────────────────────────────────────────┐
│                        TTSAlbion.exe                            │
│                                                                 │
│  ┌──────────────┐    ┌──────────────────┐    ┌───────────────┐  │
│  │  Network     │    │   Event          │    │   Message     │  │
│  │  Layer       │───▶│   Routing        │───▶│   Pipeline   │  │
│  │  (Capture)   │    │   (Dispatch)     │    │   (TTS+Audio) │  │
│  └──────────────┘    └──────────────────┘    └───────────────┘  │
│         │                    │                        │         │
│  ┌──────┴──────┐    ┌───────┴────────┐    ┌─────────┴───────┐   │
│  │ Socket      │    │ GenericEvent   │    │ IAudioSink      │   │
│  │ PacketProv  │    │ Handler        │    │ (Local/Virt/Bot)│   │
│  └─────────────┘    └────────────────┘    └─────────────────┘   │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  MainViewModel  ←→  MainWindow (WPF / MVVM)             │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

### Flujo de datos completo

```
Albion Online (UDP packets)
        │
        ▼
SocketsPacketProvider          ← Raw socket, hilo de recepción
        │  byte[]
        ▼
AlbionParser (PhotonParser)    ← Deserializa protocolo Photon
        │  EventPacket / RequestPacket / ResponsePacket
        ▼
HandlersCollection             ← Chain of Responsibility
        │
        ├──▶ GenericEventHandler
        │         │  EventPacket { EventCode = ChatMessage | ChatSay }
        │         ▼
        │    GameEventRouter    ← Mapea EventCode → handler registrado
        │         │  MessageModel / ChatSayModel
        │         ▼
        │    [filtro: usuario registrado]
        │         │  texto
        │         ▼
        │    MessageService
        │         │
        │         ├─ CommandParser.TryParse()   ← detecta prefijo "!!"
        │         │         │ payload (texto sin prefijo)
        │         │         ▼
        │         ├─ ITtsEngine.SynthesizeAsync()   ← Windows SAPI → WAV
        │         │         │ byte[] wav
        │         │         ▼
        │         └─ IAudioSink.SendAsync()         ← Local / VirtualMic / Discord
        │
        ├──▶ GenericRequestHandler   (logging / extensión futura)
        └──▶ GenericResponseHandler  (logging / extensión futura)
```

### Diagrama de capas

```
┌─────────────────────────────┐
│  Presentation               │  MainWindow.xaml / MainViewModel
│  (WPF + MVVM)               │  RelayCommand / AsyncRelayCommand
├─────────────────────────────┤
│  Application                │  MessageService / CommandParser
│  (Orchestration)            │  GenericEventHandler
├─────────────────────────────┤
│  Domain / Abstractions      │  ITtsEngine / IAudioSink / ICommandParser
│  (Interfaces)               │  IPacketHandler / IPortFilter / IPhotonParser
├─────────────────────────────┤
│  Infrastructure             │  WindowsTtsEngine / DiscordAudioSink
│  (Adapters)                 │  JsonSettingsRepository / AlbionPortResolver
│                             │  ResolvedPortFilter / SocketsPacketProvider
├─────────────────────────────┤
│  Network Library            │  HandlersCollection / PacketHandler<T>
│  (NetWorkLibrery.dll)       │  NetworkManager / ReceiverBuilder
└─────────────────────────────┘
```

---

## Herramientas y tecnologías

| Categoría | Herramienta | Versión | Rol |
|---|---|---|---|
| **Runtime** | .NET | 9 / 10 | Plataforma base |
| **UI Framework** | WPF | .NET nativo | Interfaz de usuario |
| **UI Theme** | MahApps.Metro | 2.4.10 | Ventana moderna |
| **UI Theme** | MaterialDesignThemes | 5.1.0 | Controles Material Design |
| **Network Protocol** | PhotonPackageParser | 4.1.0 | Deserialización del protocolo Photon (Albion) |
| **TTS** | System.Speech (SAPI) | 9.0.0 | Síntesis de voz nativa de Windows |
| **Audio** | NAudio | 2.3.0 | Resampling PCM, WaveOut, BufferedWaveProvider |
| **Audio codec** | Opus / Concentus | 2.2.2 | Codificación de audio para Discord |
| **Discord** | Discord.Net | 3.19.1 | Bot de voz en canal |
| **Crypto** | libsodium | 1.0.21 | Requerido por el protocolo de voz de Discord |
| **Serialización** | Newtonsoft.Json | 13.0.5 | Config JSON |
| **Raw sockets** | System.Net.Sockets | .NET nativo | Captura de paquetes UDP |
| **P/Invoke** | iphlpapi.dll | OS | Resolución de puertos por PID (`GetExtendedTcpTable`) |

---

## Patrones y decisiones de diseño

### Chain of Responsibility — `HandlersCollection` + `PacketHandler<T>`

Los paquetes de red pasan por una cadena de handlers tipados. Cada handler decide si consume el paquete (`OnHandleAsync`) o lo pasa al siguiente. Permite añadir handlers nuevos (logging, filtros, módulos futuros) sin modificar los existentes. **Cumple OCP.**

### Strategy — `IAudioSink`

El destino de audio es intercambiable en tiempo de ejecución sin tocar la pipeline TTS. Implementaciones actuales: `LocalAudioSink`, `VirtualMicAudioSink`, `DiscordAudioSink`. Añadir un sink nuevo (e.g., OBS Virtual Cam Audio) requiere solo implementar la interfaz de 1 método.

### Factory — `IAudioSinkFactory` / `DefaultAudioSinkFactory`

Centraliza la construcción de sinks y el ciclo de vida del `DiscordSocketClient`. El ViewModel no referencia ningún tipo concreto de sink — solo trabaja con la abstracción.

### Router genérico — `GenericRouterBase<TEnum>`

Mapeo `OperationCode / EventCode → handler` con suscripción tipada. Usa `Activator.CreateInstance` para hidratar modelos desde `Dictionary<byte, object>`. Evita un `switch` gigante y permite registrar handlers desde cualquier punto de composición.

### Attribute-based mapping — `[Parse(byte key)]` + `ModelHandler`

Los modelos de red declaran sus campos con un atributo que indica la clave en el diccionario de parámetros Photon. `ModelHandler` resuelve la conversión via reflection en el constructor. La reflection se ejecuta **una sola vez por instancia** al deserializar, no en hot-paths de rendering.

### Repository — `ISettingsRepository` / `JsonSettingsRepository`

Escritura atómica (temp file + rename) con `SemaphoreSlim` para concurrencia. El ViewModel nunca conoce la ruta del archivo ni el formato. Testeable con una implementación in-memory.

### Port filter — `ResolvedPortFilter`

Evita capturar todos los paquetes UDP del sistema. Resuelve los puertos del proceso Albion via `GetExtendedTcpTable` P/Invoke y cachea el resultado 15 segundos para no llamar a la API del SO en cada paquete (que puede ser cientos por segundo).

### MVVM puro

`MainWindow.xaml.cs` contiene solo `InitializeComponent` y un handler de drag. Toda la lógica vive en `MainViewModel`. Los comandos son `RelayCommand` y `AsyncRelayCommand` con `CanExecute` reactivo.

---

## Estructura del proyecto

```
TTSAlbion/
├── Albion/
│   ├── AlbionParser.cs              ← PhotonParser → HandlersCollection
│   ├── AlbionPortResolver.cs        ← IProcessPortResolver para Albion
│   ├── ProcessNetworkInspector.cs   ← P/Invoke GetExtendedTcpTable/UdpTable
│   ├── EventCodes.cs                ← Enum completo de eventos Albion
│   ├── OperationCodes.cs            ← Enum completo de operaciones Albion
│   ├── MessageSourceFilter.cs       ← [Flags] enum para filtro de chat
│   ├── Handler/
│   │   ├── GenericRouterBase<T>     ← Router tipado por enum
│   │   ├── Event/
│   │   │   ├── GenericEventHandler  ← Entry point de eventos de red
│   │   │   ├── GameEventRouter      ← Router para EventCodes
│   │   │   └── Model/               ← MessageModel, ChatSayModel, ...
│   │   ├── Request/                 ← GenericRequestHandler/Router
│   │   └── Response/                ← GenericResponseHandler/Router
│   └── Model/                       ← EventPacket, RequestPacket, ResponsePacket
├── Services/
│   ├── MessageService.cs            ← Orquesta: parse → TTS → sink
│   ├── CommandParser.cs             ← Detecta y extrae prefijo
│   └── Audio/
│       ├── LocalAudioSink.cs        ← WaveOut al altavoz por defecto
│       ├── VirtualMicAudioSink.cs   ← WaveOut a CABLE Input
│       ├── DiscordAudioSink.cs      ← PCM → Discord voice channel
│       ├── DefaultAudioSinkFactory  ← Construcción de sinks
│       └── WavToPcmConverter.cs     ← Strip WAV header / resampling
├── Infrastructure/
│   ├── JsonSettingsRepository.cs    ← Persistencia atómica en JSON
│   ├── ResamplingWavToPcmConverter  ← NAudio MediaFoundationResampler
│   └── NativeDependencyGuard.cs     ← Valida opus.dll / libsodium.dll
├── ViewModel/
│   ├── MainViewModel.cs             ← ViewModel principal (MVVM)
│   ├── RelayCommand.cs              ← ICommand síncrono
│   └── AsyncRelayCommand.cs         ← ICommand async con CanExecute
├── Interfaces/                      ← Contratos públicos
├── Converters/                      ← WPF value converters + utilidades
├── Datos/Config.cs                  ← Snapshot inmutable de configuración
└── App.xaml.cs                      ← Composition root (DI manual)

NetWorkLibrery/
├── Interfazes/
│   ├── IPacketHandler.cs            ← Contrato base de la cadena
│   ├── IPhotonParser.cs
│   └── INetworkManager.cs
└── Modelos/
    ├── HandlersCollection.cs        ← Chain of Responsibility
    ├── NetworkManager.cs            ← Fachada de arranque/parada
    ├── SocketsPacketProvider.cs     ← Raw socket + Channel<byte[]>
    ├── ReceiverBuilder.cs           ← Fluent builder de handlers
    ├── ResolvedPortFilter.cs        ← Port filter dinámico con caché
    └── StaticPortFilter.cs          ← Port filter estático (tests)
```

---

## Extensibilidad

### Añadir un nuevo sink de audio

```csharp
public sealed class MyCustomSink : IAudioSink
{
    public Task SendAsync(byte[] pcm, CancellationToken ct = default)
    {
        // tu lógica aquí
        return Task.CompletedTask;
    }
}
```

Registrarlo en `DefaultAudioSinkFactory` y añadir la opción al enum `AudioSinkType`. No se toca ninguna otra clase.

### Añadir un nuevo handler de eventos Albion

```csharp
// En el constructor de GenericEventHandler o en un handler separado:
_router.Subscribe<HarvestFinishedModel>(EventCodes.HarvestFinished, model =>
{
    // reaccionar al evento
});
```

### Añadir un motor TTS diferente (Azure, ElevenLabs...)

Implementar `ITtsEngine` y sustituirlo en el composition root (`App.xaml.cs`). Cero cambios en la pipeline.

---

## Consideraciones de seguridad

- El **Bot Token** se persiste en texto plano en `config.json`. Para producción se recomienda cifrar el campo con DPAPI o leerlo de una variable de entorno.
- La app requiere **raw sockets**, lo que implica permisos de Administrador. Esto es inherente al modelo de captura pasiva sin driver NPCAP.
- No modifica memoria del proceso de Albion ni inyecta código — es escucha de red pasiva.

---

## Créditos y licencias de terceros

| Librería | Licencia |
|---|---|
| Discord.Net | MIT |
| NAudio | Ms-PL |
| MahApps.Metro | MIT |
| MaterialDesignThemes | MIT |
| PhotonPackageParser | MIT |
| Newtonsoft.Json | MIT |
| Concentus | BSD-3 |
| VB-Audio Virtual Cable | Freeware (donationware) |
