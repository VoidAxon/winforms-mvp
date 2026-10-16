namespace WinformsMVP.MVP.Presenters
{
    public interface IInitializable
    {
        void Initialize();
    }

    public interface IInitializable<TParam>
    {
        void Initialize(TParam param);
    }
}
