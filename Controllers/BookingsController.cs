using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VenueBookingSystem.Data;
using VenueBookingSystem.Models;
using VenueBookingSystem.Services;

namespace VenueBookingSystem.Controllers
{
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ValidationService _validationService;
        private readonly ILogger<BookingsController> _logger;

        public BookingsController(
            ApplicationDbContext context,
            ValidationService validationService,
            ILogger<BookingsController> logger)
        {
            _context = context;
            _validationService = validationService;
            _logger = logger;
        }

        // GET: Bookings - Enhanced display with search
        public async Task<IActionResult> Index(string searchTerm)
        {
            var bookings = _context.vw_EnhancedBookings.AsQueryable();

            // Search functionality
            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                bookings = bookings.Where(b =>
                    b.BookingID.ToString().Contains(searchTerm) ||
                    b.EventName.ToLower().Contains(searchTerm) ||
                    b.VenueName.ToLower().Contains(searchTerm) ||
                    b.OrganizerName.ToLower().Contains(searchTerm)
                );

                ViewBag.SearchTerm = searchTerm;
                ViewBag.SearchCount = await bookings.CountAsync();

                if (!await bookings.AnyAsync())
                {
                    TempData["InfoMessage"] = $"No bookings found matching '{searchTerm}'";
                }
            }

            return View(await bookings.OrderByDescending(b => b.BookingDate).ToListAsync());
        }

        // GET: Bookings/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Venues = await _context.Venues
                .Where(v => v.IsActive)
                .OrderBy(v => v.VenueName)
                .ToListAsync();

            ViewBag.Events = await _context.Events
                .OrderBy(e => e.EventName)
                .ToListAsync();

            return View();
        }

        // POST: Bookings/Create with double-booking validation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("VenueID,EventID,BookingDate,SpecialRequests")] Booking booking)
        {
            // Remove validation errors for navigation properties
            ModelState.Remove("Venue");
            ModelState.Remove("Event");

            // Validate booking date
            if (!_validationService.IsBookingDateValid(booking.BookingDate))
            {
                ModelState.AddModelError("BookingDate", _validationService.GetDateValidationMessage(booking.BookingDate));
            }

            if (ModelState.IsValid)
            {
                // Check for double booking
                var isAvailable = await _validationService.IsVenueAvailableAsync(booking.VenueID, booking.BookingDate);

                if (!isAvailable)
                {
                    var conflictMessage = await _validationService.GetConflictMessageAsync(booking.VenueID, booking.BookingDate);
                    ModelState.AddModelError("BookingDate", conflictMessage);
                    TempData["ErrorMessage"] = conflictMessage;

                    // Re-populate dropdowns
                    ViewBag.Venues = await _context.Venues.Where(v => v.IsActive).ToListAsync();
                    ViewBag.Events = await _context.Events.ToListAsync();
                    return View(booking);
                }

                booking.CreatedAt = DateTime.Now;
                booking.Status = "Confirmed";

                _context.Add(booking);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Booking created: ID {booking.BookingID} for Venue {booking.VenueID}");
                TempData["SuccessMessage"] = $"✅ Booking created successfully! Booking ID: {booking.BookingID}";
                return RedirectToAction(nameof(Index));
            }

            // Handle model errors
            ViewBag.Venues = await _context.Venues.Where(v => v.IsActive).ToListAsync();
            ViewBag.Events = await _context.Events.ToListAsync();

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors);
                foreach (var error in errors)
                {
                    TempData["ErrorMessage"] = error.ErrorMessage;
                }
            }

            return View(booking);
        }

        // GET: Bookings/Delete
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var booking = await _context.vw_EnhancedBookings
                .FirstOrDefaultAsync(b => b.BookingID == id);

            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        // POST: Bookings/Delete - Cancel booking instead of hard delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);

            if (booking != null)
            {
                booking.Status = "Cancelled";
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Booking {id} cancelled");
                TempData["SuccessMessage"] = "Booking cancelled successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Bookings/Details
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var booking = await _context.vw_EnhancedBookings
                .FirstOrDefaultAsync(b => b.BookingID == id);

            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }
    }
}