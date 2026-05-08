using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VenueBookingSystem.Data;
using VenueBookingSystem.Models;
using VenueBookingSystem.Services;

namespace VenueBookingSystem.Controllers
{
    public class VenuesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ValidationService _validationService;
        private readonly IBlobStorageService _blobStorageService;
        private readonly ILogger<VenuesController> _logger;

        public VenuesController(
            ApplicationDbContext context,
            ValidationService validationService,
            IBlobStorageService blobStorageService,
            ILogger<VenuesController> logger)
        {
            _context = context;
            _validationService = validationService;
            _blobStorageService = blobStorageService;
            _logger = logger;
        }

        // GET: Venues
        public async Task<IActionResult> Index()
        {
            return View(await _context.Venues.OrderBy(v => v.VenueName).ToListAsync());
        }

        // GET: Venues/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Venues/Create with image upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("VenueName,Location,Capacity")] Venue venue, IFormFile? imageFile)
        {
            ModelState.Remove("ImageUrl");
            ModelState.Remove("Bookings");

            // Validate required fields manually for better error messages
            if (string.IsNullOrWhiteSpace(venue.VenueName))
            {
                ModelState.AddModelError("VenueName", "Venue Name is required.");
            }

            if (string.IsNullOrWhiteSpace(venue.Location))
            {
                ModelState.AddModelError("Location", "Location is required.");
            }

            if (venue.Capacity <= 0)
            {
                ModelState.AddModelError("Capacity", "Capacity must be greater than 0.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Upload image if provided
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        venue.ImageUrl = await _blobStorageService.UploadImageAsync(imageFile, "venueimages");
                        _logger.LogInformation($"Uploaded image for venue: {venue.VenueName}");
                    }

                    venue.CreatedAt = DateTime.Now;
                    venue.IsActive = true;

                    _context.Add(venue);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"✅ Venue '{venue.VenueName}' created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (ArgumentException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                    TempData["ErrorMessage"] = ex.Message;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating venue");
                    ModelState.AddModelError("", $"Error creating venue: {ex.Message}");
                    TempData["ErrorMessage"] = $"Error creating venue: {ex.Message}";
                }
            }

            return View(venue);
        }

        // GET: Venues/Delete
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var venue = await _context.Venues
                .FirstOrDefaultAsync(m => m.VenueID == id);

            if (venue == null)
            {
                return NotFound();
            }

            // Check if venue can be deleted
            var canDelete = await _validationService.CanDeleteVenueAsync(venue.VenueID);
            ViewBag.CanDelete = canDelete;

            if (!canDelete)
            {
                TempData["ErrorMessage"] = $"⚠️ Cannot delete venue '{venue.VenueName}' because it has active bookings.";
            }

            return View(venue);
        }

        // POST: Venues/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var venue = await _context.Venues.FindAsync(id);

            if (venue != null)
            {
                // Check validation again
                var canDelete = await _validationService.CanDeleteVenueAsync(id);

                if (!canDelete)
                {
                    TempData["ErrorMessage"] = "Cannot delete venue with active bookings.";
                    return RedirectToAction(nameof(Index));
                }

                // Delete image from blob storage if exists
                if (!string.IsNullOrEmpty(venue.ImageUrl))
                {
                    await _blobStorageService.DeleteImageAsync(venue.ImageUrl, "venueimages");
                }

                _context.Venues.Remove(venue);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Venue deleted: {venue.VenueName}");
                TempData["SuccessMessage"] = $"✅ Venue '{venue.VenueName}' deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Venues/Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var venue = await _context.Venues.FindAsync(id);
            if (venue == null)
            {
                return NotFound();
            }

            return View(venue);
        }

        // POST: Venues/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("VenueID,VenueName,Location,Capacity,IsActive")] Venue venue, IFormFile? imageFile)
        {
            if (id != venue.VenueID)
            {
                return NotFound();
            }

            ModelState.Remove("ImageUrl");
            ModelState.Remove("Bookings");
            ModelState.Remove("CreatedAt");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingVenue = await _context.Venues.FindAsync(id);
                    if (existingVenue == null)
                    {
                        return NotFound();
                    }

                    // Upload new image if provided
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        // Delete old image if exists
                        if (!string.IsNullOrEmpty(existingVenue.ImageUrl))
                        {
                            await _blobStorageService.DeleteImageAsync(existingVenue.ImageUrl, "venueimages");
                        }

                        existingVenue.ImageUrl = await _blobStorageService.UploadImageAsync(imageFile, "venueimages");
                    }

                    existingVenue.VenueName = venue.VenueName;
                    existingVenue.Location = venue.Location;
                    existingVenue.Capacity = venue.Capacity;
                    existingVenue.IsActive = venue.IsActive;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"✅ Venue '{venue.VenueName}' updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!VenueExists(venue.VenueID))
                    {
                        return NotFound();
                    }
                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(venue);
        }

        private bool VenueExists(int id)
        {
            return _context.Venues.Any(e => e.VenueID == id);
        }
    }
}