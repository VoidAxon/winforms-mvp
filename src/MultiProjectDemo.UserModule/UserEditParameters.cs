namespace MultiProjectDemo.UserModule
{
    /// <summary>
    /// Runtime parameters for <see cref="UserEditPresenter"/>.
    /// <see cref="UserId"/> == 0 means "create a new user"; any other value loads an
    /// existing user for editing.
    /// </summary>
    public class UserEditParameters
    {
        public int UserId { get; set; }
    }

    public class UserEditResult
    {
        public int UserId { get; set; }
        public string Name { get; set; }
    }
}
