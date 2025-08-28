using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Phichat.Application.DTOs.User
{
    public class AvatarUploadRequest
    {
        public IFormFile? File { get; set; }
    }
}

