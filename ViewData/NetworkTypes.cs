using System.IO;

namespace WalkerSim.Viewer
{
    public interface IWalkerSimMessage
    {
        void Serialize(Stream stream);
        void Deserialize(Stream stream);
    }
}
