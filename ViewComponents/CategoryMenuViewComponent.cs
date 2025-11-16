using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;
using MovieWeb.Models.Entities;
using MovieWeb.Models.ViewModels;

namespace MovieWeb.ViewComponents
{
    public class CategoryMenuViewComponent : ViewComponent
    {
        private readonly MovieWebDbContext _context;

        public CategoryMenuViewComponent(MovieWebDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Lấy category active, trừ "Phim Bộ", sắp xếp theo CategoryId
            var categories = await _context.Categories
                .Where(c => (c.IsActive ?? false) && c.Name != "Phim Bộ")

                .OrderBy(c => c.CategoryId)
                .ToListAsync();

            // Chia đều 4 cột
            int colCount = 4;
            int itemsPerCol = (int)Math.Ceiling((double)categories.Count / colCount);

            var model = new CategoryMenuViewModel
            {
                Categories = categories,
                ColCount = colCount,
                ItemsPerCol = itemsPerCol
            };

            return View(model);
        }
    }

    
}
