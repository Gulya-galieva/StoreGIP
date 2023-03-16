using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Linq;

namespace GIPManager
{
    /// <summary>
    /// Препоставляет методы для работы с Таблицей пользователей в БД
    /// </summary>
    public class UsersManager
    {
        StoreContext db;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        public UsersManager(StoreContext context)
        {
            db = context;
        }

        /// <summary>
        /// Добавляет нового пользователя в БД
        /// </summary>
        /// <param name="user">Инфа о новом пользователе</param>
        /// <param name="idWorker"></param>
        public void AddUser(User user, int idWorker)
        {
            user.Password = GetMd5Hash(MD5.Create(), user.Password);
            user.WorkerID = idWorker;
            db.Users.Add(user);
            db.SaveChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="worker"></param>
        /// <returns></returns>
        public int AddWorker(Worker worker)
        {
            db.Workers.Add(worker);
            db.SaveChanges();
            return (worker.Id);
        }

        /// <summary>
        /// Удаляем пользователя из БД по Id
        /// </summary>
        /// <param name="userId">Id пользователя, которого нужно удалить из БД</param>
        public void DeleteUser(int userId)
        {
            var user = db.Users.Find(userId);
            //Delete linked comments and unread flags
            var data = from u in db.UnreadSubstationComments
                       join c in db.CommentSubstations on u.CommentSubstationId equals c.Id
                       where c.UserId == user.Id
                       select new { u, c };
            db.UnreadSubstationComments.RemoveRange(data.Select(d => d.u));
            db.CommentSubstations.RemoveRange(data.Select(d => d.c));
            //Delete user
            db.Users.Remove(user);
            db.SaveChanges();
        }

        /// <summary>
        /// Удаляем рабочего из БД по Id
        /// </summary>
        /// <param name="userId"></param>
        public void DeleteWorker(int userId)
        {
            var worker = db.Workers.Find(userId);
            db.Workers.Remove(worker);
            db.SaveChanges();
        }

        //Обновить инфу
        /// <summary>
        /// Изменить инфу пользователя в БД (Логин, e-mail, Имя).
        /// </summary>
        /// <param name="userId">Id пользователя, данные которого нужно изменить в БД</param>
        /// <param name="login">Новый Логин (используется для входа в систему)</param>
        /// <param name="eMail">Новый e-mail</param>
        /// <param name="name">Новое Имя пользователя (не логин, просто инфо)</param>
        public void UpdateUserInfo(int userId, string login, string eMail, string name)
        {
            var userOld = db.Users.Find(userId);
            if(userOld != null)
            {
                if (login != null) userOld.Login = login;
                if (eMail != null) userOld.Email = eMail;
                if (name != null) userOld.Name = name;
                //if (status != null) userOld.StatusId = status;
                db.SaveChanges();
            }
        }

        
        /// <summary>
        /// Изменить инфу рабочего в БД (Имя).
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="name"></param>
        public void UpdateWorkerInfo(int userId, string name)
        {
            var worker = db.Workers.Find(userId);
            if(worker != null)
            {
                string[] fioList = name.Split(' ');
                worker.Surname = fioList[0];
                worker.Name = fioList[1];
                worker.MIddlename = fioList[2];
                db.Workers.Update(worker);
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Установить новый пароль
        /// </summary>
        /// <param name="userId">Id пользователя, данные которого нужно изменить в БД</param>
        /// <param name="newPassword">Новый пароль</param>
        public void UpdatePass(int userId, string newPassword)
        {
            var userOld = db.Users.Find(userId);
            if (userOld != null)
            {
                if (newPassword != null)
                    userOld.Password = GetMd5Hash(MD5.Create(), newPassword);
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Изменение роли
        /// </summary>
        /// <param name="userId">Id пользователя, данные которого нужно изменить в БД</param>
        /// <param name="newRoleId">Id новой роли</param>
        public void UpdateRole(int userId, int newRoleId)
        {
            var user = db.Users.Find(userId);
            var role = db.Roles.Find(newRoleId);

            if (user != null && role != null)
            {
                user.RoleId = newRoleId;
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Изменение тип рабочего
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="newRoleId"></param>
        public void UpdateType(int userId, int newRoleId)
        {
            var user = db.Users.Find(userId);
            var worker = db.Workers.Find(user.WorkerID);
            switch (newRoleId)
            {
                case 15:
                    worker.WorkerTypeId = 1;
                    break;
                default:
                    break;
            }
            db.Workers.Update(worker);
            db.SaveChanges();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        public void AddImageForUser(User user)
        {
            if(user != null)
            {
                db.Users.Update(user);
                db.SaveChanges();
            }
        }

        static string GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash.
            // Преобразуйте входную строку в массив байтов и вычислите хэш.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            // Создайте новый Stringbuilder для сбора байтов
            // и создайте строку.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            // Перебрать каждый байт хешированных данных
            // и отформатировать каждый как шестнадцатеричную строку.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            // Вернуть шестнадцатеричную строку.
            return sBuilder.ToString();
        }
    }
}
