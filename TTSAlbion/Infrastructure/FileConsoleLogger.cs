using System.IO;
using System.Text;

namespace TTSAlbion.Infrastructure;

public static class FileConsoleLogger
{
    private static readonly object Sync = new();
    private static TextWriter? _fileWriter;
    private static TextWriter? _originalOut;
    private static TextWriter? _originalError;

    public static string? LogFilePath { get; private set; }

    public static void Initialize(string logFilePath)
    {
        lock (Sync)
        {
            if (_fileWriter is not null)
                return;

            var directory = Path.GetDirectoryName(logFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            _originalOut = Console.Out;
            _originalError = Console.Error;

            var stream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            _fileWriter = TextWriter.Synchronized(new TimestampTextWriter(stream));
            LogFilePath = logFilePath;

            var teeWriter = TextWriter.Synchronized(new TeeTextWriter(_originalOut, _fileWriter));
            var errorWriter = TextWriter.Synchronized(new TeeTextWriter(_originalError, _fileWriter));

            Console.SetOut(teeWriter);
            Console.SetError(errorWriter);

            Console.WriteLine($"[Log] Logger inicializado en {logFilePath}");
        }
    }

    private sealed class TeeTextWriter : TextWriter
    {
        private readonly TextWriter _left;
        private readonly TextWriter _right;

        public TeeTextWriter(TextWriter left, TextWriter right)
        {
            _left = left;
            _right = right;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            _left.Write(value);
            _right.Write(value);
        }

        public override void Write(string? value)
        {
            _left.Write(value);
            _right.Write(value);
        }

        public override void WriteLine(string? value)
        {
            _left.WriteLine(value);
            _right.WriteLine(value);
        }

        public override Task WriteLineAsync(string? value)
            => Task.WhenAll(_left.WriteLineAsync(value), _right.WriteLineAsync(value));

        public override void Flush()
        {
            _left.Flush();
            _right.Flush();
        }
    }

    private sealed class TimestampTextWriter : TextWriter
    {
        private readonly StreamWriter _writer;
        private bool _atLineStart = true;

        public TimestampTextWriter(Stream stream)
        {
            _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true
            };
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (_atLineStart)
            {
                _writer.Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ");
                _atLineStart = false;
            }

            _writer.Write(value);
            if (value == '\n')
                _atLineStart = true;
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            foreach (var ch in value)
                Write(ch);
        }

        public override void WriteLine(string? value)
        {
            Write(value);
            Write(Environment.NewLine);
        }
    }
}
