namespace TTSAlbion.ViewModel;

public class LanguageItem
{
    public string Code { get; }
    public string Name { get; }

    public LanguageItem(string code, string name)
    {
        Code = code;
        Name = name;
    }
}