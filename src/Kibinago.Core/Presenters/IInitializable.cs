namespace Kibinago.Presenters
{
    public interface IInitializable
    {
        void Initialize();
    }

    public interface IInitializable<in TParam>
    {
        void Initialize(TParam param);
    }
}
