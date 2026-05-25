using System.Collections.Generic;

namespace MultiProjectDemo.UserModule
{
    public interface IUserRepository
    {
        IReadOnlyList<User> GetAll();
        User GetById(int id);
        void Save(User user);
        void Delete(int id);
    }
}
