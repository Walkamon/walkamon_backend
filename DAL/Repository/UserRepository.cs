//using DAL.Data;
//using DAL.Interfaces;
//using DAL.Model;
//using DAL.Repositories;
//using Microsoft.EntityFrameworkCore;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DAL.Repository
//{
//    public class UserRepository
//        : GenericRepository<User>,
//        IUserRepository
//    {
//        public UserRepository(WalkamonContext context)
//            : base(context)
//        {
//        }

//        public async Task<User?> GetByUsernameAsync(
//            string username)
//        {
//            return await _context.Users
//                .Include(x => x.Roles)
//                .FirstOrDefaultAsync(
//                    x => x.Username == username
//                );
//        }

//        public async Task<User?> GetByEmailAsync(
//            string email)
//        {
//            return await _context.Users
//                .FirstOrDefaultAsync(
//                    x => x.Email == email
//                );
//        }
//    }
//}
