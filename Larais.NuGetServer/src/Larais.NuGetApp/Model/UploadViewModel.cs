﻿using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Larais.NuGetApp.Model
{
    public class UploadViewModel
    {
        [Required(ErrorMessage = "Please select a valid package")]
        [Display(Name = "Browse")]
        public IFormFile File { get; set; }
    }
}