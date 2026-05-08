using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VenueBookingSystem.Data;
using VenueBookingSystem.Models;
using VenueBookingSystem.Services;

namespace VenueBookingSystem.Controllers
{
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ValidationService _validationService;
        private readonly ILogger<EventsController> _logger;

        public EventsController(
            ApplicationDbContext context,
            ValidationService validationService,
            ILogger<EventsController> logger)
        {
            _context = context;
            _validationService = validationService;
            _logger = logger;
        }

        // GET: Events
        public async Task<IActionResult> Index()
        {
            return View(await _context.Events.OrderBy(e => e.EventDate).ToListAsync());
        }

        // GET: Events/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Events/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EventName,Description,EventDate,DurationHours,OrganizerName")] Event @event)
        {
            ModelState.Remove("Bookings");

            // Validate event date
            if (@event.EventDate < DateTime.Now)
            {
                ModelState.AddModelError("EventDate", "Event date cannot be in the past.");
            }

            if (ModelState.IsValid)
            {
                @event.CreatedAt = DateTime.Now;
                _context.Add(@event);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Event created: {@event.EventName}");
                TempData["SuccessMessage"] = $"✅ Event '{@event.EventName}' created successfully!";
                return RedirectToAction(nameof(Index));
            }

            return View(@event);
        }

        // GET: Events/Delete
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events
                .FirstOrDefaultAsync(m => m.EventID == id);

            if (@event == null)
            {
                return NotFound();
            }

            // Check if event can be deleted
            var canDelete = await _validationService.CanDeleteEventAsync(@event.EventID);
            ViewBag.CanDelete = canDelete;

            if (!canDelete)
            {
                TempData["ErrorMessage"] = $"⚠️ Cannot delete event '{@event.EventName}' because it has active bookings.";
            }

            return View(@event);
        }

        // POST: Events/Delete
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @event = await _context.Events.FindAsync(id);

            if (@event != null)
            {
                var canDelete = await _validationService.CanDeleteEventAsync(id);

                if (!canDelete)
                {
                    TempData["ErrorMessage"] = "Cannot delete event with active bookings.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Events.Remove(@event);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Event deleted: {@event.EventName}");
                TempData["SuccessMessage"] = $"✅ Event '{@event.EventName}' deleted successfully!";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Events/Edit
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events.FindAsync(id);
            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // POST: Events/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("EventID,EventName,Description,EventDate,DurationHours,OrganizerName")] Event @event)
        {
            if (id != @event.EventID)
            {
                return NotFound();
            }

            ModelState.Remove("Bookings");
            ModelState.Remove("CreatedAt");

            if (ModelState.IsValid)
            {
                try
                {
                    var existingEvent = await _context.Events.FindAsync(id);
                    if (existingEvent == null)
                    {
                        return NotFound();
                    }

                    existingEvent.EventName = @event.EventName;
                    existingEvent.Description = @event.Description;
                    existingEvent.EventDate = @event.EventDate;
                    existingEvent.DurationHours = @event.DurationHours;
                    existingEvent.OrganizerName = @event.OrganizerName;

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"✅ Event '{@event.EventName}' updated successfully!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EventExists(@event.EventID))
                    {
                        return NotFound();
                    }
                    throw;
                }

                return RedirectToAction(nameof(Index));
            }

            return View(@event);
        }

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.EventID == id);
        }
    }
}