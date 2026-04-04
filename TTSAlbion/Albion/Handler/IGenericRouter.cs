using RequipAlbion.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace RequipAlbion.Network.Handler
{
    public interface IGenericRouter<TEnum> where TEnum : Enum
    {
        public void Subscribe<TModel>(TEnum code, Action<TModel> handler);
        public bool TryRoute(TEnum code, IReadOnlyDictionary<byte, object> parameters);
    }
}
