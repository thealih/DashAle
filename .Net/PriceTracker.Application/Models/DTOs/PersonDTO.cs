using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PriceTracker.Application.Models.DTOs
{
    public class PersonDTO
    {
        public PIdDto PId { get; set; } = new PIdDto();
        public int PersonCode { get; set; } // باید رشته‌های عددی رو قبول کنه
        public string FirstName { get; set; }
        public string FullName { get; set; }
        public DateTime Tavalod { get; set; }
        public List<UsersDto> Users { get; set; } = new List<UsersDto>();
        public Typess? UType { get; set; }
        public Sex? Sexx { get; set; }
    }

    public enum Typess
    {
        Student = 1,
        Faculty = 2
    }

    public enum Sex
    {
        Male,
        Female = 2
    }

    public class PIdDto
    {
        public int Id { get; set; } // باید رشته‌های عددی رو قبول کنه
        public bool? Gender { get; set; } // "female" -> true, "male" -> false, بقیه -> null
        public DateTime? BirthDate { get; set; } // باید رشته‌های تاریخ رو قبول کنه
    }

    public class UsersDto
    {
        public string Username { get; set; } // باید اعداد رو به رشته تبدیل کنه
    }
}
