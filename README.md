# TTSAlbion

> Text-to-Speech bridge para Albion Online. Escucha el chat del juego y vocaliza mensajes por altavoz local, micrófono virtual o bot de Discord.

---

## Descarga

Actualmente se distribuye una sola versión:

👉 **TTSAlbion.exe**

- `Framework-Dependent`
- Requiere `.NET Desktop Runtime 9 x64`
- Requiere `Python 3`
- El script del bot de Discord va embebido dentro del ejecutable

---

## ¿Qué hace?

TTSAlbion:

1. Escucha paquetes de red del cliente de Albion Online.
2. Extrae mensajes de chat relevantes.
3. Filtra por jugador registrado.
4. Detecta un prefijo configurable, por defecto `!!`.
5. Genera audio TTS con Windows SAPI.
6. Envía el audio al destino configurado:
   - `Local`
   - `VirtualMic`
   - `DiscordBot`

Caso típico:

Escribes `!! atacar base norte` en Albion y el texto se vocaliza automáticamente.

---

## Requisitos

### Obligatorios

| Dependencia | Motivo | Descarga |
|---|---|---|
| `.NET Desktop Runtime 9 x64` | Runtime de la app | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/9.0) |
| `Python 3` | Bot de voz de Discord | [python.org/downloads](https://www.python.org/downloads/) |
| `Albion Online` | Fuente de mensajes | [albiononline.com](https://albiononline.com) |

### Opcional

| Dependencia | Motivo | Descarga |
|---|---|---|
| `VB-Audio Virtual Cable` | Necesario para `VirtualMic` | [vb-audio.com/Cable](https://vb-audio.com/Cable/) |

### Importante sobre TTS

La síntesis actual usa `System.Speech` (`Windows SAPI`).

Eso significa que Windows debe tener:

- voces SAPI instaladas,
- componentes de Speech habilitados.

En sistemas modificados o recortados, como algunos `MiniOS`, puede pasar que:

- la aplicación abra,
- Discord funcione,
- pero no se genere audio porque Windows no tiene voces disponibles.

En ese caso revisa `ttsalbion.log` junto al ejecutable.

---

## Instalación

1. Descarga `TTSAlbion.exe`.
2. Instala `.NET Desktop Runtime 9 x64`.
3. Instala `Python 3`.
4. Ejecuta `TTSAlbion.exe` como **Administrador**.
5. Configura el jugador, prefijo y salida de audio en la UI.

### Dependencias de Python para Discord

El bot de Discord necesita `discord.py[voice]`.

La aplicación intenta instalarlo automáticamente al arrancar el bot.

Si la instalación automática falla, ejecuta:

```powershell
python -m pip install "discord.py[voice]>=2.4,<3.0"
```

### VirtualMic en Discord

Si quieres enviar audio a Discord como micrófono:

1. Instala `VB-Audio Virtual Cable`.
2. En Discord selecciona `CABLE Output` como micrófono.
3. En TTSAlbion selecciona `VirtualMic`.

---

## Requisitos de ejecución

- Windows 10 / 11
- Ejecutar como **Administrador**

La app usa captura de red a bajo nivel, por eso necesita elevación.

---

## Logs

Los logs se crean junto al ejecutable:

- `ttsalbion.log`
- `discord-python-bridge.log`

Son los primeros archivos que debes revisar si:

- no se escucha nada,
- el bot de Discord no arranca,
- o el TTS no genera audio.

---

## Configuración en la UI

| Campo | Descripción |
|---|---|
| `Jugador` | Nombre del personaje a observar |
| `Prefijo` | Texto que activa TTS, por defecto `!!` |
| `Salida de audio` | `Local`, `VirtualMic` o `DiscordBot` |
| `Token / User ID` | Solo para `DiscordBot` |

---

## Arquitectura resumida

```text
Albion packets
  -> parser Photon
  -> event handler
  -> MessageService
  -> ITtsEngine (Windows SAPI)
  -> IAudioSink
     -> Local
     -> VirtualMic
     -> DiscordBot (Python bridge)
```

### Componentes clave

- `MainViewModel`: lógica de UI
- `MessageService`: cola y pipeline TTS
- `WindowsTtsEngine`: síntesis con `System.Speech`
- `DiscordAudioSink`: puente hacia Python y Discord
- `JsonSettingsRepository`: persistencia en `Datos/config.json`

---

## Tecnologías

| Categoría | Herramienta | Rol |
|---|---|---|
| UI | WPF | Interfaz |
| TTS | `System.Speech` | Voz de Windows |
| Audio | `NAudio` | Resampling / salida |
| Discord voice | `Python 3` + `discord.py[voice]` | Bot de voz |
| Red | `PhotonPackageParser` | Parser de protocolo Albion |
| Config | `Newtonsoft.Json` | Persistencia |

---

## Seguridad

- La app **no modifica** Albion Online.
- Solo escucha tráfico de red de forma pasiva.
- Puede generar falsos positivos de antivirus por captura de red.
- El token del bot se guarda en `config.json`; para producción conviene protegerlo mejor.

---

## Créditos

| Librería | Licencia |
|---|---|
| Discord.Net | MIT |
| NAudio | Ms-PL |
| MaterialDesignThemes | MIT |
| Newtonsoft.Json | MIT |
| PhotonPackageParser | MIT |
| VB-Audio Virtual Cable | Freeware / donationware |

## Agradecimientos

Un agradecimiento especial a [Triky313](https://github.com/Triky313) por su proyecto AlbionOnline-StatisticsAnalysis, el cual sirvió como referencia e inspiración para parte de la implementación.

Repositorio:
https://github.com/Triky313/AlbionOnline-StatisticsAnalysis
