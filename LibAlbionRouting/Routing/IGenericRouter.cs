namespace LibAlbionRouting.Routing
{
    public interface IGenericRouter<TEnum> where TEnum : Enum
    {
        public void Subscribe<TModel>(TEnum code, Action<TModel> handler);
        public bool TryRoute(TEnum code, IReadOnlyDictionary<byte, object> parameters);
    }
}
