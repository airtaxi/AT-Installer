namespace InstallerCommons.ZipHelper
{
    public class ActionProgress<T>(Action<T> handler) : IProgress<T>
    {
        private readonly Action<T> _handler = handler;

        void IProgress<T>.Report(T value)
        {
            _handler(value);
        }
    }
}
