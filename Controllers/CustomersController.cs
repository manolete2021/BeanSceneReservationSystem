using BeanSceneReservationSystem.Data;
using BeanSceneReservationSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace BeanSceneReservationSystem.Controllers;

[Authorize(Roles = "Manager,Staff")]
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;

    public CustomersController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.CustomerMembers.ToListAsync());
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var customerMember = await _context.CustomerMembers
            .FirstOrDefaultAsync(c => c.CustomerID == id);

        if (customerMember == null)
        {
            return NotFound();
        }

        return View(customerMember);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("FirstName,LastName,Email,Phone")] CustomerMember customerMember)
    {
        ValidateCustomer(customerMember);

        if (ModelState.IsValid)
        {
            customerMember.UserID = null;
            _context.Add(customerMember);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return View(customerMember);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var customerMember = await _context.CustomerMembers.FindAsync(id);

        if (customerMember == null)
        {
            return NotFound();
        }

        return View(customerMember);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("CustomerID,FirstName,LastName,Email,Phone")] CustomerMember customerMember)
    {
        if (id != customerMember.CustomerID)
        {
            return NotFound();
        }

        ValidateCustomer(customerMember);

        if (ModelState.IsValid)
        {
            var existingCustomer = await _context.CustomerMembers.FindAsync(id);

            if (existingCustomer == null)
            {
                return NotFound();
            }

            existingCustomer.FirstName = customerMember.FirstName;
            existingCustomer.LastName = customerMember.LastName;
            existingCustomer.Email = customerMember.Email;
            existingCustomer.Phone = customerMember.Phone;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        return View(customerMember);
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var customerMember = await _context.CustomerMembers
            .FirstOrDefaultAsync(c => c.CustomerID == id);

        if (customerMember == null)
        {
            return NotFound();
        }

        return View(customerMember);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var customerMember = await _context.CustomerMembers.FindAsync(id);

        if (customerMember == null)
        {
            return RedirectToAction(nameof(Index));
        }

        var hasReservations = await _context.Reservations
            .AnyAsync(r => r.CustomerID == customerMember.CustomerID);

        if (hasReservations)
        {
            ModelState.AddModelError(
                string.Empty,
                "This customer cannot be deleted because they have existing reservations.");
            return View(customerMember);
        }

        _context.CustomerMembers.Remove(customerMember);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            _context.Entry(customerMember).State = EntityState.Unchanged;
            ModelState.AddModelError(
                string.Empty,
                "This customer cannot be deleted because they have existing reservations.");
            return View(customerMember);
        }

        return RedirectToAction(nameof(Index));
    }

    private void ValidateCustomer(CustomerMember customerMember)
    {
        if (string.IsNullOrWhiteSpace(customerMember.FirstName))
        {
            ModelState.AddModelError(nameof(customerMember.FirstName), "First Name is required.");
        }

        if (string.IsNullOrWhiteSpace(customerMember.LastName))
        {
            ModelState.AddModelError(nameof(customerMember.LastName), "Last Name is required.");
        }

        if (string.IsNullOrWhiteSpace(customerMember.Email))
        {
            ModelState.AddModelError(nameof(customerMember.Email), "Email is required.");
        }
        else if (!new EmailAddressAttribute().IsValid(customerMember.Email))
        {
            ModelState.AddModelError(nameof(customerMember.Email), "Please enter a valid email address.");
        }

        if (string.IsNullOrWhiteSpace(customerMember.Phone))
        {
            ModelState.AddModelError(nameof(customerMember.Phone), "Phone is required.");
        }
    }
}
