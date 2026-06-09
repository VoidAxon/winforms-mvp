using System.Collections.Generic;
using System.Linq;

namespace WinformsMVP.Samples.CascadeDemo
{
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public override string ToString() { return Name; }
    }

    public class SubCategory
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string Name { get; set; }
        public override string ToString() { return Name; }
    }

    public class Product
    {
        public int Id { get; set; }
        public int SubCategoryId { get; set; }
        public string Name { get; set; }
        public override string ToString() { return Name; }
    }

    public interface ICategoryRepository { IList<Category> GetAll(); }
    public interface ISubCategoryRepository { IList<SubCategory> GetByCategory(int categoryId); }
    public interface IProductRepository { IList<Product> GetBySubCategory(int subCategoryId); }

    /// <summary>In-memory sample data: 2 categories x 2 subcategories x 2 products.</summary>
    public sealed class InMemoryCatalog :
        ICategoryRepository, ISubCategoryRepository, IProductRepository
    {
        private readonly List<Category> _categories = new List<Category>
        {
            new Category { Id = 1, Name = "Electronics" },
            new Category { Id = 2, Name = "Books" },
        };

        private readonly List<SubCategory> _subCategories = new List<SubCategory>
        {
            new SubCategory { Id = 11, CategoryId = 1, Name = "Laptops" },
            new SubCategory { Id = 12, CategoryId = 1, Name = "Phones" },
            new SubCategory { Id = 21, CategoryId = 2, Name = "Fiction" },
            new SubCategory { Id = 22, CategoryId = 2, Name = "Tech" },
        };

        private readonly List<Product> _products = new List<Product>
        {
            new Product { Id = 111, SubCategoryId = 11, Name = "UltraBook 14" },
            new Product { Id = 112, SubCategoryId = 11, Name = "WorkStation 17" },
            new Product { Id = 121, SubCategoryId = 12, Name = "Phone X" },
            new Product { Id = 122, SubCategoryId = 12, Name = "Phone Mini" },
            new Product { Id = 211, SubCategoryId = 21, Name = "The Novel" },
            new Product { Id = 212, SubCategoryId = 21, Name = "Short Stories" },
            new Product { Id = 221, SubCategoryId = 22, Name = "Clean Code" },
            new Product { Id = 222, SubCategoryId = 22, Name = "The Pragmatic Programmer" },
        };

        public IList<Category> GetAll() { return _categories.ToList(); }
        public IList<SubCategory> GetByCategory(int categoryId)
        {
            return _subCategories.Where(s => s.CategoryId == categoryId).ToList();
        }
        public IList<Product> GetBySubCategory(int subCategoryId)
        {
            return _products.Where(p => p.SubCategoryId == subCategoryId).ToList();
        }
    }
}
