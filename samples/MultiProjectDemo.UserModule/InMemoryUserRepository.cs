using System.Collections.Generic;
using System.Linq;

namespace MultiProjectDemo.UserModule
{
    /// <summary>
    /// In-memory store for demo purposes. Clones on read and write so callers cannot
    /// mutate the stored entity by side effect.
    /// </summary>
    public class InMemoryUserRepository : IUserRepository
    {
        private readonly Dictionary<int, User> _users = new Dictionary<int, User>();
        private int _nextId = 1;

        public InMemoryUserRepository()
        {
            Save(new User { Name = "Alice", Email = "alice@example.com" });
            Save(new User { Name = "Bob",   Email = "bob@example.com"   });
            Save(new User { Name = "Carol", Email = "carol@example.com" });
        }

        public IReadOnlyList<User> GetAll()
            => _users.Values.OrderBy(u => u.Id).Select(Clone).ToList();

        public User GetById(int id)
            => _users.TryGetValue(id, out var u) ? Clone(u) : null;

        public void Save(User user)
        {
            if (user.Id == 0) user.Id = _nextId++;
            _users[user.Id] = Clone(user);
        }

        public void Delete(int id) => _users.Remove(id);

        private static User Clone(User u)
            => new User { Id = u.Id, Name = u.Name, Email = u.Email };
    }
}
