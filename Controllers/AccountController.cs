using BeanSceneReservationSystem.Data;
using BeanSceneReservationSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BeanSceneReservationSystem.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
    }

    [AllowAnonymous]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // New registered users always receive the Member role.
        var roleResult = await _userManager.AddToRoleAsync(user, "Member");
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "The account could not be created. Please try again.");
            return View(model);
        }

        var customer = new CustomerMember
        {
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Email = model.Email.Trim(),
            Phone = model.Phone.Trim(),
            // Link this customer record to the new login account.
            UserID = user.Id
        };

        try
        {
            _context.CustomerMembers.Add(customer);
            await _context.SaveChangesAsync();
        }
        catch (Exception)
        {
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "The account could not be created. Please try again.");
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // The selected profile must match the user's real role.
        if (!await _userManager.IsInRoleAsync(user, model.SelectedRole))
        {
            ModelState.AddModelError(string.Empty, "The selected profile does not match this account.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        // End the current login session.
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
