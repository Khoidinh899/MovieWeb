using MovieWeb.Models.Entities;
using System.Collections.Generic;

namespace MovieWeb.ViewModels
{
    public class CategoryMenuViewModel
    {
        public List<Category> Categories { get; set; } = new();
        public int ColCount { get; set; }
        public int ItemsPerCol { get; set; }
    }
}
