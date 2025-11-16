using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieWeb.Data;

namespace MovieWeb.Views.Shared.Components.CountryMenu
{
    public class CountryMenuViewComponent : ViewComponent
    {
        private readonly MovieWebDbContext _context;

        public CountryMenuViewComponent(MovieWebDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var countries = await _context.Countries
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View(countries);
        }
    }
}