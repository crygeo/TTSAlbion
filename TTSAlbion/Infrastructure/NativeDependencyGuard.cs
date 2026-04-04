// TTSAlbion/Infrastructure/NativeDependencyGuard.cs
using System.Runtime.InteropServices;

namespace TTSAlbion.Infrastructure;

/// <summary>
/// Verifica en startup que las DLLs nativas requeridas por Discord.Net audio
/// estén presentes y sean de la arquitectura correcta.
/// Falla rápido con un mensaje claro en lugar de BadImageFormatException profunda.
/// </summary>
public static class NativeDependencyGuard
{
    [DllImport("opus", EntryPoint = "opus_get_version_string",
               CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr OpusVersionString();

    [DllImport("libsodium", EntryPoint = "sodium_version_string",
               CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SodiumVersionString();

    public static void Verify()
    {
        try
        {
            var opus = Marshal.PtrToStringAnsi(OpusVersionString());
            Console.WriteLine($"[NativeGuard] opus OK → {opus}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"opus.dll no encontrada o arquitectura incorrecta. " +
                $"Asegúrate de tener opus.dll x64 en el directorio de ejecución. ({ex.Message})", ex);
        }

        try
        {
            var sodium = Marshal.PtrToStringAnsi(SodiumVersionString());
            Console.WriteLine($"[NativeGuard] libsodium OK → {sodium}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"libsodium.dll no encontrada o arquitectura incorrecta. " +
                $"Asegúrate de tener libsodium.dll x64 en el directorio de ejecución. ({ex.Message})", ex);
        }
    }
}