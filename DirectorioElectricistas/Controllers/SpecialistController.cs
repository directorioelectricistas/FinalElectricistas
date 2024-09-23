using DirectorioElectricistas.Models;
using DirectorioElectricistas.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Mono.TextTemplating;
using OfficeOpenXml;
using System.Collections.Generic;

namespace DirectorioElectricistas.Controllers
{
    public class SpecialistController : Controller
    {
        private ISpecialistCollection db = new SpecialistCollection();

        //Users version

        

        // GET: SpecialistController
        public ActionResult Index(string searchTerm, string department, string municipality, List<string> states)
        {
            // Obtener todos los especialistas aprobados
            List<Specialist> specialists = db.GetAllSpecialists()
                                             .Where(s => s.State == "Aprobado")
                                             .ToList();

            // Filtrar por término de búsqueda si se proporciona
            if (!string.IsNullOrEmpty(searchTerm))
            {
                specialists = specialists
                    .Where(s => s.Name.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Filtrar por departamento si se proporciona
            if (!string.IsNullOrEmpty(department))
            {
                specialists = specialists
                    .Where(s => s.Place.Equals(department, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Filtrar por municipio si se proporciona
            if (!string.IsNullOrEmpty(municipality))
            {
                specialists = specialists
                    .Where(s => s.MainPlace.Equals(municipality, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Filtrar por estados si se proporcionan
            if (states != null && states.Any())
            {
                specialists = specialists
                    .Where(s => states.Contains(s.State))
                    .ToList();
            }

            // Verificar si hay resultados
            bool hasResults = specialists.Any();

            // Pasar el indicador de resultados a la vista
            ViewBag.HasResults = hasResults;

            return View(specialists);
        }




        // GET: SpecialistController/Details/5
        public ActionResult Details(string id)
        {
            var specialist = db.GetSpecialistById(id);
            return View(specialist);
        }

        // GET: SpecialistController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: SpecialistController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Specialist specialist, IFormFile imageFile)
        {
            try
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".jfif" };
                    var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(extension))
                    {
                        ModelState.AddModelError("ImageFile", "Ingrese un tipo de imagen válido.");
                        return View(specialist);
                    }

                    using (var ms = new MemoryStream())
                    {
                        imageFile.CopyTo(ms);
                        var imageBytes = ms.ToArray();
                        specialist.ImageUrl = Convert.ToBase64String(imageBytes);
                    }
                }
                else
                {
                    ModelState.AddModelError("ImageFile", "Ingrese un tipo de imagen válido.");
                    return View(specialist);
                }

                var existingSpecialistApproved = db.GetSpecialistByCardIdAndState(specialist.CardId, "Aprobado");
                var existingSpecialistPending = db.GetSpecialistByCardIdAndState(specialist.CardId, "Pendiente");
                var existingSpecialistRejected = db.GetSpecialistByCardIdAndState(specialist.CardId, "Rechazado");

                if (existingSpecialistApproved != null)
                {
                    ModelState.AddModelError("CardId", "Ya existe un especialista aprobado con este número de tarjeta profesional.");
                    return View(specialist);
                }

                if (existingSpecialistPending != null)
                {
                    ModelState.AddModelError("CardId", "Hay un especialista con esta tarjeta profesional en espera de aprobación.");
                    return View(specialist);
                }

                if (existingSpecialistRejected != null)
                {
                    if (existingSpecialistPending != null)
                    {
                        ModelState.AddModelError("CardId", "No se puede agregar más registros con esta tarjeta profesional, ya hay uno en espera de aprobación.");
                        return View(specialist);
                    }
                }

                db.InsertSpecialist(specialist);

                // Guardar el mensaje de éxito en TempData
                TempData["SuccessMessage"] = "El registro fue realizado con éxito y está pendiente de aprobación. Se le notificará a su correo electrónico una vez aprobado.";

                // Regresar a la vista Create para mostrar el modal
                return View(specialist);
            }
            catch
            {
                return View(specialist);
            }
        }






        // GET: SpecialistController/Edit/5
        public ActionResult Edit(string id)
        {

            var specialist = db.GetSpecialistById(id);
            return View(specialist);
        }

        // POST: SpecialistController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, IFormCollection collection, IFormFile imageFile, string CurrentImageUrl)
        {
            try
            {
                // Obtener el especialista actual de la base de datos
                var existingSpecialist = db.GetSpecialistById(id);
                if (existingSpecialist == null)
                {
                    return NotFound();
                }

                // Verificar si ya existe un especialista con el nuevo CardId, excluyendo el especialista actual
                var newCardId = collection["CardId"];
                var existingSpecialistWithCardId = db.GetSpecialistByCardId(newCardId);

                // Comparar si existe un especialista con el mismo CardId pero diferente ID
                if (existingSpecialistWithCardId != null && existingSpecialistWithCardId.Id != existingSpecialist.Id)
                {
                    ModelState.AddModelError("CardId", "Ya existe un especialista con este número de tarjeta profesional.");
                    return View(existingSpecialist);  // Regresar a la vista con el mensaje de error
                }

                // Actualizar solo los campos editables
                existingSpecialist.Name = collection["Name"];
                existingSpecialist.CardId = newCardId;
                existingSpecialist.Number = collection["Number"];
                existingSpecialist.Place = collection["Place"];
                existingSpecialist.MainPlace = collection["MainPlace"];

                // Manejar la subida de la imagen
                if (imageFile != null && imageFile.Length > 0)
                {
                    // Obtener el directorio donde se guardará la imagen
                    var imagesDirectory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

                    // Verificar si el directorio existe, si no, crearlo
                    if (!Directory.Exists(imagesDirectory))
                    {
                        Directory.CreateDirectory(imagesDirectory);
                    }

                    // Guardar la imagen en la ruta especificada
                    var filePath = Path.Combine(imagesDirectory, Path.GetFileName(imageFile.FileName));

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        imageFile.CopyTo(stream);
                    }

                    existingSpecialist.ImageUrl = "/images/" + Path.GetFileName(imageFile.FileName);  // Ruta relativa
                }
                else
                {
                    // Si no se sube una nueva imagen, mantener la imagen actual
                    existingSpecialist.ImageUrl = CurrentImageUrl;
                }

                // Actualizar el especialista en la base de datos
                db.UpdateSpecialist(existingSpecialist);

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }


        // Approve
        [HttpPost]
        public async Task<IActionResult> Approve(string id)
        {
            var specialist = db.GetSpecialistById(id);
            if (specialist == null)
            {
                return NotFound();
            }

            // Obtener todos los especialistas
            var allSpecialists = db.GetAllSpecialists();

            // Verificar si ya existe otro especialista con el mismo CardId
            var conflictingSpecialists = allSpecialists
                .Where(s => s.CardId == specialist.CardId && s.Id != specialist.Id && s.State != "No aprobado")
                .ToList();

            if (conflictingSpecialists.Any())
            {
                TempData["ErrorMessage"] = "Ya existe un especialista con el mismo número de tarjeta profesional y su estado no permite aprobación.";
                return RedirectToAction(nameof(Index));
            }

            // Actualizar el estado del especialista a "Aprobado"
            specialist.State = "Aprobado";
            db.UpdateSpecialist(specialist);

            return RedirectToAction(nameof(Index));
        }






        //No approve
        [HttpPost]
        public async Task<IActionResult> ChangeState(string id, string newState)
        {
            var specialist = db.GetSpecialistById(id);
            if (specialist == null)
            {
                return NotFound();
            }

            // Actualizar el estado del especialista
            specialist.State = newState;
            db.UpdateSpecialist(specialist);

            // No redirigir, simplemente actualizar el estado
            return RedirectToAction(nameof(Edit), new { id = specialist.Id });
        }







        // GET: SpecialistController/Delete/5
        public ActionResult Delete(string id)
        {
            var specialist = db.GetSpecialistById(id);
            return View(specialist);
        }

        // POST: SpecialistController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id, IFormCollection collection)
        {
            try
            {
                db.DeleteSpecialist(id);

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }


        
    }
}
