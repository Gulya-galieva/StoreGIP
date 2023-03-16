using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Reflection;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace GIPManager
{
    //
    // bool Worker.IsWorkerType()
    // void Worker.SetWorkerTypeId()
    //
    // bool Device.IsCurrentState()
    // bool Device.IsEnableToAddTU()
    // void Device.AddState()
    //
    // void RegPoint.AddAction()
    // string RegPoint.Remove()
    //
    // void Substation.AddAction()
    // string Substation.AddRegPoint()
    // string Substation.Remove()
    //
    // void NetRegion.AddAction()
    // string NetRegion.AddSubstation()
    //
    // string OrderTable.AddOrder()
    /// <summary>
    /// Класс для методов расширений
    /// </summary>
    public static class Helper
    {
        #region Расширения Worker
        /// <summary>
        /// Проверка типа (или должности) работника 
        /// </summary>
        /// <returns>Возвращает true если тип совпадает с workerTypeName</returns>
        public static bool IsWorkerType(this Worker worker, WorkerTypeName workerTypeName)
        {
            return worker.WorkerType.Description == workerTypeName.ToString();
        }
        /// <summary>
        /// Задать работнику его тип (или должность) из WorkerTypeName.[ТипНаВыбор]
        /// Устанавливает только внешний ключ WorkerTypeId.
        /// </summary>
        public static void SetWorkerTypeId(this Worker worker, WorkerTypeName workerTypeName)
        {
            worker.WorkerTypeId = EnumsHelper.GetWorkerTypeId(workerTypeName);
        }

        /// <summary> Получить полное имя работника в формате "[Фамилия] [Имя] [Отчество]" </summary>
        /// <param name="worker"></param>
        /// <returns></returns>
        public static string GetFullName(this Worker worker)
        {
            return worker.Surname + " " + worker.Name + " " + worker.MIddlename;
        }
        #endregion

        #region Расширения Device
        /// <summary>
        /// Проверка текущего состояния счетчика
        /// </summary>
        /// <returns>Возвращает true если состояние совпадает с deviceStateTypeName</returns>
        public static bool IsCurrentState(this Device device, DeviceStateTypeName deviceStateTypeName)
        {
            return device.CurrentState == deviceStateTypeName.ToString();
        }
        /// <summary>
        /// Проверяет можно ли устройство (ПУ) прикрепить к ТУ (точке учета)
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public static bool IsEnableToAddTU(this Device device)
        {
            return (device.CurrentState == DeviceStateTypeName.Outcome.ToString() ||
                    device.CurrentState == DeviceStateTypeName.AddToReport.ToString());
        }
        /// <summary>
        /// Проверяет можно ли устройство (ПУ) импортировать (точке учета)
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        public static bool IsEnableToImportTU(this Device device)
        {
            return device.RegPoints.FirstOrDefault(r => r.Status != RegPointStatus.Demounted) == null;
        }
        /// <summary>
        /// Добавить действие над Оборудованием
        /// </summary>
        /// <param name="device"></param>
        /// <param name="typeName">Тип действия из списка</param>
        /// <param name="userId">Id пользователя который совершает действие</param>
        /// <param name="comment">Если не нужен текстовый комментарий, то подствьте null</param>
        public static void AddState(this Device device, DeviceStateTypeName typeName, int userId, string comment)
        {
            //Новый статус в историю
            device.DeviceStates.Add(new DeviceState() { DeviceStateTypeId = EnumsHelper.GetDeviceStateTypeId(typeName), UserId = userId, Comment = comment, Date = DateTime.Now });

            string typeNameString = typeName.ToString();
            //Новый текущий статус (!) Важный момент
            //Этапы, имя которых совпадает должно совпадать с текущим статусом
            if (typeNameString == DeviceStateTypeName.Income.ToString() ||
               typeNameString == DeviceStateTypeName.ToTune.ToString() ||
               typeNameString == DeviceStateTypeName.ToAssembly.ToString() ||
               typeNameString == DeviceStateTypeName.Outcome.ToString() ||
               typeNameString == DeviceStateTypeName.ReturnToMnfc.ToString() ||
               typeNameString == DeviceStateTypeName.Defect.ToString() ||
               typeNameString == DeviceStateTypeName.FromAssembly.ToString() ||
               typeNameString == DeviceStateTypeName.FromTune.ToString() ||
               typeNameString == DeviceStateTypeName.AddToReport.ToString() ||
               typeNameString == DeviceStateTypeName.AcceptedByCurator.ToString() ||
               typeNameString == DeviceStateTypeName.AddToTU.ToString())
                device.CurrentState = typeName.ToString();

            //Этапы после которых текущий статус равен "выдача со склада"
            if (typeNameString == DeviceStateTypeName.DeleteFromReport.ToString() ||
                typeNameString == DeviceStateTypeName.DeleteFromTU.ToString())
                device.CurrentState = DeviceStateTypeName.Outcome.ToString();

            //Этапы после которых текущий статус равен "прием на склад"
            if (typeNameString == DeviceStateTypeName.ReturnToStore.ToString() ||
                typeNameString == DeviceStateTypeName.DefectDelete.ToString())
                device.CurrentState = DeviceStateTypeName.Income.ToString();
        }
        #endregion

        #region Расширения RegPoint
        /// <summary>
        /// Добавить действие над Точкой учета
        /// </summary>
        /// <param name="regPoint"></param>
        /// <param name="actionTypeName">Тип действия из списка</param>
        /// <param name="userId">Id пользователя который совершает действие</param>
        /// <param name="comment">Если не нужен текстовый комментарий, то подствьте null</param>
        public static void AddAction(this RegPoint regPoint, ActionTypeName actionTypeName, int userId, string comment)
        {
            //Добавляем в подстанцию действия
            regPoint.RegPointActions.Add(new RegPointAction() { ActionTypeId = EnumsHelper.GetActionId(actionTypeName), UserId = userId, Comment = comment, Date = DateTime.Now });
        }
        /// <summary> Удаляет точку учета и всю связанную с ней информацию (если она добавлена вручную) </summary>
        /// <param name="rp"></param>
        /// <param name="userId">Id пользователя который удаляет точку учета</param>
        /// <returns></returns>
        public static string Remove(this RegPoint rp, int userId)
        {
            if (rp == null) return "Ошибка: Не удалось найти ТУ";

            using (StoreContext db = new StoreContext())
            {
                //Для того чтобы не затрагивать внешний контекст ищем объект в БД еще раз
                var regPoint = db.RegPoints.Find(rp.Id);
                if (regPoint == null) return "Ошибка: Не удалось найти ТУ";
                //Выставляем статус у прикрепленного устройства - "отвязано от ТУ"
                if (regPoint.Device != null)
                    regPoint.Device.AddState(DeviceStateTypeName.DeleteFromTU, userId,
                        regPoint.Substation.Name + " " + regPoint.InstallAct.InstallPlaceType.Name + regPoint.InstallAct.InstallPlaceNumber + " " + regPoint.Consumer.O_Street + " " + regPoint.Consumer.O_House);
                //Событие удаления в историю
                regPoint.Substation.AddAction(ActionTypeName.RemoveRegPoint, userId,
                    regPoint.Substation.Name + " [" + regPoint.Device?.SerialNumber + "] " + regPoint.InstallAct.InstallPlaceType.Name + regPoint.InstallAct.InstallPlaceNumber + " " + regPoint.Consumer.O_Street + " " + regPoint.Consumer.O_House);
                //Вся инфа по ТУ также удалится
                if(regPoint.RegPointFlags.ReportedByMounter) return "Запрет на удаление. Точка импортирована из электронного отчета";
                db.RegPoints.Remove(regPoint);
                db.SaveChanges();
                rp = null;
                return "Точка учета успешно удалена из базы";
            }
        }
        /// <summary> Удаляет точку учета и всю связанную с ней информацию (даже если импортирована из отчета) </summary>
        /// <param name="rp"></param>
        /// <param name="userId">Id пользователя который удаляет точку учета</param>
        /// <returns></returns>
        public static string RemoveForce(this RegPoint rp, int userId)
        {
            if (rp == null) return "Ошибка: Не удалось найти ТУ";

            using (StoreContext db = new StoreContext())
            {
                //Для того чтобы не затрагивать внешний контекст ищем объект в БД еще раз
                var regPoint = db.RegPoints.Find(rp.Id);
                if (regPoint == null) return "Ошибка: Не удалось найти ТУ";
                //Выставляем статус у прикрепленного устройства - "отвязано от ТУ"
                if (regPoint.Device != null)
                    regPoint.Device.AddState(DeviceStateTypeName.DeleteFromTU, userId,
                        regPoint.Substation.Name + " " + regPoint.InstallAct.InstallPlaceType.Name + regPoint.InstallAct.InstallPlaceNumber + " " + regPoint.Consumer.O_Street + " " + regPoint.Consumer.O_House);
                //Событие удаления в историю
                regPoint.Substation.AddAction(ActionTypeName.RemoveRegPoint, userId,
                    regPoint.Substation.Name + " [" + regPoint.Device?.SerialNumber + "] " + regPoint.InstallAct.InstallPlaceType.Name + regPoint.InstallAct.InstallPlaceNumber + " " + regPoint.Consumer.O_Street + " " + regPoint.Consumer.O_House);
                //Вся инфа по ТУ также удалится
                db.RegPoints.Remove(regPoint);
                db.SaveChanges();
                rp = null;
                return "Точка учета успешно удалена из базы";
            }
        }
        #endregion

        #region Расширения Substation
        /// <summary>
        /// Добавить действие над Подстанцией
        /// </summary>
        /// <param name="substation"></param>
        /// <param name="actionTypeName">Тип действия из списка</param>
        /// <param name="userId">Id пользователя который совершает действие</param>
        /// <param name="comment">Если не нужен текстовый комментарий, то подствьте null</param>
        public static void AddAction(this Substation substation, ActionTypeName actionTypeName, int userId, string comment)
        {
            //Добавляем в подстанцию действия
            substation.SubstationActions.Add(new SubstationAction() { ActionTypeId = EnumsHelper.GetActionId(actionTypeName), UserId = userId, Comment = comment, Date = DateTime.Now });
        }
        /// <summary>
        /// Добавляет новую точку учета и создает все связанные записи в БД. Так же добавляет действие Create для этой точки.
        /// </summary>
        /// <param name="substation"></param>
        /// <param name="deviceId">Id привязанного к точке прибора учета</param>
        /// <param name="userId">Id пользователя, который создает эту точку учета</param>
        public static string AddRegPoint(this Substation substation, int deviceId, int userId)
        {
            return substation.AddRegPoint(deviceId, userId, null, null, null);
        }
        /// <summary>
        /// Добавляет новую точку учета и создает все связанные записи в БД. Так же добавляет действие Create для этой точки.
        /// </summary>
        /// <param name="substation"></param>
        /// <param name="deviceId">Id привязанного к точке прибора учета</param>
        /// <param name="userId">Id пользователя, который создает эту точку учета</param>
        /// <param name="regPointFlagsData">Объект с флагами. Если нет первоначальных данных, то отправляй null</param>
        /// <param name="installActData">Инфа для акта. Если нет первоначальных данных, то отправляй null</param>
        /// <param name="consumerData">Инфа о потребителе. Если нет первоначальных данных, то отправляй null</param>
        public static string AddRegPoint(this Substation substation, int deviceId, int userId, RegPointFlags regPointFlagsData, InstallAct installActData, Consumer consumerData)
        {
            //Связанные таблицы InstallAct, RegPointFlags, Consumer создаются в конструкторе RegPoint
            //Создаем записи в связанных таблицах
            RegPoint regPoint = new RegPoint();

            //Обязательно метку о том кто создал и когда
            regPoint.RegPointActions.Add(new RegPointAction() { ActionTypeId = EnumsHelper.GetActionId(ActionTypeName.Create), UserId = userId, Date = DateTime.Now });

            //Проверим статус устройства и привяжем его к точке учета
            using (StoreContext db = new StoreContext())
            {
                var device = db.Devices.FirstOrDefault(d => d.Id == deviceId);
                if (device == null) return "Этого прибора учета нет в базе";
                device.AddState(DeviceStateTypeName.AddToTU, userId, null);
                regPoint.InstallAct = installActData ?? new InstallAct() { InstallPlaceTypeId = EnumsHelper.GetInstallPlaceTypeId(InstallPlaceTypeName.Undefined) };
                regPoint.RegPointFlags = regPointFlagsData ?? new RegPointFlags();
                regPoint.Consumer = consumerData ?? new Consumer();
                regPoint.DeviceId = device.Id;
                substation.RegPoints.Add(regPoint);
                substation.AddAction(ActionTypeName.AddRegPoint, userId, null);
                if (device.DeviceType.Type.ToLower() == "успд")
                    regPoint.Status = RegPointStatus.USPD;
                db.SaveChanges();
                return "Точка учета создана";
            }
        }
        /// <summary>
        /// Удаляет подстанцию и всю связанную с ней информацию (действия и точки учета)
        /// </summary>
        /// <param name="sub"></param>
        /// <param name="userId">Id пользователя который удаляет подстанцию</param>
        /// <returns></returns>
        public static string Remove(this Substation sub, int userId)
        {
            if (sub == null) return "Ошибка: Не удалось найти подстанцию";

            using (StoreContext db = new StoreContext())
            {
                //Для того чтобы не затрагивать внешний контекст ищем объект в БД еще раз
                var substation = db.Substations.Find(sub.Id);
                if (substation == null) return "Ошибка: Не удалось найти подстанцию";
                //Выставляем статусы у всех прикрепленных устройств - "отвязано от ТУ"
                foreach (var point in substation.RegPoints)
                {
                    if (point.Device != null)
                        point.Device.AddState(DeviceStateTypeName.DeleteFromTU, userId,
                            point.Substation.Name + " " + point.InstallAct.InstallPlaceType.Name + point.InstallAct.InstallPlaceNumber + " " + point.Consumer.O_Street + " " + point.Consumer.O_House);
                }
                //Все привязанные к подстанции точки учета удалятся тоже (и вся инфа по этим ТУ также удалится)
                substation.NetRegion.AddAction(ActionTypeName.RemoveSubstation, userId, substation.Name);
                db.Substations.Remove(substation);
                db.SaveChanges();
                sub = null;
                return "Подстанция успешно удалена из базы";
            }
        }
        #endregion

        #region Расширения NetRegion
        /// <summary>
        /// Добавляет действие на Регионом (районом)
        /// </summary>
        /// <param name="netRegion"></param>
        /// <param name="actionTypeName">Тип действия из списка</param>
        /// <param name="userId">Id пользователя который совершает действие</param>
        /// <param name="comment">Если не нужен текстовый комментарий, то подствьте null</param>
        public static void AddAction(this NetRegion netRegion, ActionTypeName actionTypeName, int userId, string comment)
        {
            //Добавляем в район действия
            netRegion.NetRegionActions.Add(new NetRegionAction() { ActionTypeId = EnumsHelper.GetActionId(actionTypeName), UserId = userId, Comment = comment, Date = DateTime.Now });
        }
        /// <summary>
        /// Проверяет есть ли подстанция с таким же именем, и если нет, то добавляет и создает действие Create для подстанции. Данные добавляются только в контекст, db.SaveChanges() нужно вызывать вручную.
        /// </summary>
        /// <param name="netRegion"></param>
        /// <param name="substationName">Имя новой подстанции</param>
        /// <param name="userId">Id пользователя, который выполняет это действие</param>
        /// <returns>Возвращает сообщение (string) о результате выполнения</returns>
        public static string AddSubstation(this NetRegion netRegion, string substationName, int userId)
        {
            if (netRegion == null) return "Ошибка: Не удалось загрузить Район";

            Substation newSubstation = new Substation();
            newSubstation.Name = substationName;
            newSubstation.SubstationStateId = 1;
            //Обязательно метку о том кто создал и когда
            newSubstation.AddAction(ActionTypeName.Create, userId, null);

            using (StoreContext db = new StoreContext())
            {
                //Проверим есть ли подстанция с таким названием
                var sub = db.Substations.FirstOrDefault(s => s.Name == substationName);
                if (sub != null) return substationName + " уже есть в этом районе";
                netRegion.Substations.Add(newSubstation);
                netRegion.AddAction(ActionTypeName.AddSubstation, userId, substationName);
                return substationName + " успешно добавлена в " + netRegion.Name;
            }
        }
        #endregion

        #region Расширения Reports
        /// <summary>
        /// Изменить статус отчета ВЛ
        /// </summary>
        /// <param name="report">Изменяемый отчет</param>
        /// <param name="stateTypeName">Новый тип состояния отчета</param>
        public static void ChangeState(this MounterReportUgesAL report, ReportStateTypeName stateTypeName)
        {
            //Изменение типа состояния отчета
            report.ReportStateId = EnumsHelper.GetReportStateId(stateTypeName);
        }

        /// <summary>
        /// Изменить статус отчета ТП/РП
        /// </summary>
        /// <param name="report">Изменяемый отчет</param>
        /// <param name="stateTypeName">Новый тип состояния отчета</param>
        public static void ChangeState(this SBReport report, ReportStateTypeName stateTypeName)
        {
            //Изменение типа состояния отчета
            report.ReportStateId = EnumsHelper.GetReportStateId(stateTypeName);
        }

        /// <summary>
        /// Изменить статус отчета УСПД
        /// </summary>
        /// <param name="report">Изменяемый отчет</param>
        /// <param name="stateTypeName">Новый тип состояния отчета</param>
        public static void ChangeState(this USPDReport report, ReportStateTypeName stateTypeName)
        {
            //Изменение типа состояния отчета
            report.ReportStateId = EnumsHelper.GetReportStateId(stateTypeName);
        }

        /// <summary>
        /// Изменить статус отчета Демонтажа
        /// </summary>
        /// <param name="report">Изменяемый отчет</param>
        /// <param name="stateTypeName">Новый тип состояния отчета</param>
        public static void ChangeState(this UnmountReport report, ReportStateTypeName stateTypeName)
        {
            //Изменение типа состояния отчета
            report.ReportStateId = EnumsHelper.GetReportStateId(stateTypeName);
        }
        #endregion

        #region Расширения Consumer
        /// <summary>
        /// 
        /// </summary>
        /// <param name="consumer"></param>
        /// <param name="Local"></param>
        /// <param name="Local_Secondary"></param>
        /// <param name="Street"></param>
        /// <param name="House"></param>
        /// <param name="Build"></param>
        /// <param name="Flat"></param>
        /// <returns></returns>
        public static string FormatAddress(this Consumer consumer, string Local, string Local_Secondary, string Street, string House, string Build, string Flat)
        {
            string result = "";
            if (Local != "" && Local != null) result += Local;
            if (Local_Secondary != "" && Local_Secondary != null) result += ", " + Local_Secondary;
            if (Street != "" && Street != null) result += ", ул. " + Street;
            if (House != "" && House != "-" && House != "0" && House != null) result += ", д. " + House;
            if (Build != "" && Build != "-" && Build != "0" && Build != null) result += "/" + Build;
            if (Flat != "" && Flat != "-" && Flat != "0" && Flat != null) result += ", кв. " + Flat;

            return result;
        }

        /// <summary>
        /// Получить отформатированный Адрес объекта потребителя
        /// </summary>
        /// <param name="consumer"></param>
        /// <returns></returns>
        public static string FormatOAddress(this Consumer consumer)
        {
            return consumer.FormatAddress(consumer.O_Local, consumer.O_Local_Secondary, consumer.O_Street, consumer.O_House, consumer.O_Build, consumer.O_Flat);
        }

        /// <summary>
        /// Получить отформатированный юридический Адрес потребителя
        /// </summary>
        /// <param name="consumer"></param>
        /// <returns></returns>
        public static string FormatUAddress(this Consumer consumer)
        {
            return consumer.FormatAddress(consumer.U_Local, consumer.U_Local_Secondary, consumer.U_Street, consumer.U_House, consumer.U_Build, consumer.U_Flat);
        }
        #endregion

        #region Расширения PaymentReport
        /// <summary>
        /// Приводит период отчета в формат "с [начало периода] по [конец периода]"
        /// </summary>
        /// <param name="report"></param>
        /// <returns></returns>
        public static string GetPeriodString(this PaymentReport report)
        {
            var datePeriodEnd = report.DatePeriodStart.Day == 1 ?
                   new DateTime(report.DatePeriodStart.Year, report.DatePeriodStart.Month, 15) :
                   new DateTime(report.DatePeriodStart.Year, report.DatePeriodStart.Month, 1).AddDays(-1);
                   /*new DateTime(report.DatePeriodStart.Year, report.DatePeriodStart.Month + 1, 1).AddDays(-1);*/
            return "c " + report.DatePeriodStart.ToString("dd MMMM yyyy") + " по " + datePeriodEnd.ToString("dd MMMM yyyy");
        }
        #endregion

        #region Расширение OrderTable

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderTable"></param>
        /// <param name="itemid"></param>
        /// <param name="comment"></param>
        /// <param name="timeid"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        //public static string EditOrder(this OrderTable orderTable, int itemid, string comment, int timeid)
        public static string EditOrder(this OrderTable orderTable, int itemid, string comment, int timeid, string user)
        {
            // Диспетчер
            if (orderTable == null) return "Ошибка: Не удалось загрузить Заявку";
            StoreContext DB = new StoreContext();
            orderTable = DB.OrderTable.FirstOrDefault(s => s.Id == itemid);
            string users = (from a in DB.Users
                            join b in DB.Roles on a.RoleId equals b.Id
                            where a.Login == user
                            select b.Name).First();
            if (users == "dispatcher")
            {
                if (comment != null && timeid == 6)
                {
                    orderTable.Id_OrderStatus = 6;
                }
                if (timeid != 6 && comment == null)
                {
                    orderTable.Id_OrderStatus = 4;
                    orderTable.Id_TermApplication = timeid;
                }
                //if (comment == null && timeid == 6)
                //{
                //    orderTable.Id_OrderStatus = 3;
                //}
            }
            if (users == "master")
            {
                if (comment == null && timeid == 6)
                {
                    orderTable.Id_OrderStatus = 3;
                }
            }

            using (StoreContext db = new StoreContext())
            {
                db.OrderTable.Update(orderTable);
                db.SaveChanges();
                return "Заявка успешно отправлено!";
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderTablePermission"></param>
        /// <param name="itemid"></param>
        /// <param name="comment"></param>
        /// <param name="workerid"></param>
        /// <param name="user"></param>
        /// <param name="timeid"></param>
        /// <returns></returns>
        public static string EditOrder(this OrderTablePermission orderTablePermission, int itemid, string comment, int workerid, string user, int timeid)
        {
            // Подписанты
            if (orderTablePermission == null) return "Ошибка: Не удалось загрузить Заявку";
            StoreContext DB = new StoreContext();
            int id = Convert.ToInt32((from a in DB.OrderTablePermission
                                      where a.IdOrder == itemid
                                      select a.Id).FirstOrDefault());

            orderTablePermission = DB.OrderTablePermission.FirstOrDefault(s => s.Id == id);
            var orderTable = DB.OrderTable.FirstOrDefault(s => s.Id == itemid);
            string users = (from a in DB.Users
                            join b in DB.Roles on a.RoleId equals b.Id
                            where a.Login == user
                            select b.Name).First();
            string username = (from a in DB.Users
                               where a.Login == user
                               select a.Name).FirstOrDefault();

            //orderTablePermission = DB.OrderTablePermission.FirstOrDefault(s => s.Id == id);
            if (users == "dispatcher")
            {
                if (comment != null)
                {
                    orderTablePermission.DispatcherId = workerid;
                    orderTablePermission.DispatcherComment = username + ": " + comment;
                    orderTablePermission.Notification = 1;
                }

                if (comment == null)
                {
                    orderTablePermission.DispatcherId = workerid;
                    orderTablePermission.Notification = 3;
                    switch (timeid)
                    {
                        case 1:
                            orderTablePermission.Term = DateTime.Now.AddHours(12);
                            break;

                        case 2:
                            orderTablePermission.Term = DateTime.Now.AddHours(24);
                            break;

                        case 3:
                            orderTablePermission.Term = DateTime.Now.AddHours(36);
                            break;

                        case 4:
                            orderTablePermission.Term = DateTime.Now.AddHours(48);
                            break;

                        case 5:
                            orderTablePermission.Term = DateTime.Now.AddHours(72);
                            break;

                        default:
                            break;
                    }

                }
                orderTablePermission.Date = DateTime.Now;
                using (StoreContext db = new StoreContext())
                {
                    db.OrderTablePermission.Update(orderTablePermission);
                    db.SaveChanges();
                    return "Заявка успешно отправлено!";
                }
            }
            if (users == "podpisant")
            {
                if (orderTablePermission.PodpisantOneId == 0 || (orderTablePermission.PodpisantOneId == workerid && comment != null))
                {
                    if (comment == null)
                    {
                        orderTablePermission.PodpisantOneId = workerid;
                        using (StoreContext db = new StoreContext())
                        {
                            db.OrderTablePermission.Update(orderTablePermission);
                            db.SaveChanges();
                        }
                    }
                    if (comment != null)
                    {
                        orderTablePermission.PodpisantOneId = workerid;
                        orderTablePermission.PodpisantOneComment = username + ": " + comment;
                        orderTable.Id_OrderStatus = 5;
                        orderTable.Color = "#f00000";
                        orderTablePermission.Notification = 1;
                        using (StoreContext db = new StoreContext())
                        {
                            db.OrderTable.Update(orderTable);
                            db.OrderTablePermission.Update(orderTablePermission);
                            db.SaveChanges();
                        }
                    }
                    return "Заявка успешно отправлено!";
                }

                if (orderTablePermission.PodpisantTwoId == 0 || (orderTablePermission.PodpisantTwoId == workerid && comment != null))
                {
                    if (comment == null)
                    {
                        orderTablePermission.PodpisantTwoId = workerid;
                        using (StoreContext db = new StoreContext())
                        {
                            db.OrderTablePermission.Update(orderTablePermission);
                            db.SaveChanges();
                        }
                    }
                    if (comment != null)
                    {
                        orderTablePermission.PodpisantTwoId = workerid;
                        orderTablePermission.PodpisantTwoComment = username + ": " + comment;
                        orderTable.Id_OrderStatus = 5;
                        orderTable.Color = "#f00000";
                        orderTablePermission.Notification = 1;
                        using (StoreContext db = new StoreContext())
                        {
                            db.OrderTable.Update(orderTable);
                            db.OrderTablePermission.Update(orderTablePermission);
                            db.SaveChanges();

                        }
                    }
                    return "Заявка успешно отправлено!";

                }

                if (orderTablePermission.PodpisantThreeId == 0 || (orderTablePermission.PodpisantThreeId == workerid && comment != null))
                {
                    if (comment == null)
                    {
                        orderTablePermission.PodpisantThreeId = workerid;
                        orderTable.Id_OrderStatus = 8;
                        orderTable.Color = "#00ff00";
                        orderTablePermission.Notification = 1;
                    }
                    if (comment != null)
                    {
                        orderTablePermission.PodpisantThreeId = workerid;
                        orderTablePermission.PodpisantThreeComment = username + ": " + comment;
                        orderTable.Id_OrderStatus = 5;
                        orderTable.Color = "#f00000";
                        orderTablePermission.Notification = 1;
                    }
                    orderTablePermission.Date = DateTime.Now;
                    
                    using (StoreContext db = new StoreContext())
                    {
                        db.OrderTable.Update(orderTable);
                        db.OrderTablePermission.Update(orderTablePermission);
                        db.SaveChanges();
                        return "Заявка одобрена";
                    }
                    //AlertTable alertTable = new AlertTable();
                    //alertTable.AddAlert("Ответ по заявке", workerid, orderTable.Id_Worker, "/AppBasePage", orderTable.Id);
                   
                }
            }

            return "";

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderTablePermission"></param>
        /// <param name="itemid"></param>
        /// <param name="comment"></param>
        /// <param name="user"></param>
        /// <param name="workerid"></param>
        /// <returns></returns>
        public static string AddOrder(this OrderTablePermission orderTablePermission, int itemid, string comment, string user, int workerid)
        {
            StoreContext DB = new StoreContext();

            var searchidorder = DB.OrderTablePermission.FirstOrDefault(s => s.IdOrder == itemid);

            string users = (from a in DB.Users
                            join b in DB.Roles on a.RoleId equals b.Id
                            where a.Login == user
                            select b.Name).First();

            if (searchidorder == null)
            {
                //orderTablePermission.Id = DB.OrderTablePermission.Count() + 1;
                orderTablePermission.IdOrder = itemid;


                if (comment != null && users == "dispatcher")
                {
                    orderTablePermission.DispatcherId = workerid;
                    orderTablePermission.DispatcherComment = comment;
                }

                if (comment == null && users == "dispatcher")
                {
                    orderTablePermission.DispatcherId = workerid;

                }

                orderTablePermission.Date = DateTime.Now;
                orderTablePermission.Notification = 2;

                using (StoreContext db = new StoreContext())
                {
                    db.OrderTablePermission.Add(orderTablePermission);
                    db.SaveChanges();
                }
            }
            return "Заявка успешно отправлено!";
        }

        /// <summary>
        /// Добавление заявки
        /// </summary>
        /// <param name="orderTable"></param>
        /// <param name="description"></param>
        /// <param name="type"></param>
        /// <param name="Id_Sub"></param>
        /// <param name="id_work"></param>
        /// <param name="startDate"></param>
        /// <param name="end"></param>
        /// <param name="worker_one"></param>
        /// <param name="worker_two"></param>
        /// <param name="worker_three"></param>
        /// <returns></returns>
        public static string AddOrder(this OrderTable orderTable, string description, int type, int Id_Sub, int id_work, DateTime startDate, DateTime end, int worker_one, int worker_two, int worker_three)
        {
            if (orderTable == null) return "Ошибка: Не удалось загрузить Заявку";

            StoreContext DB = new StoreContext();


            //orderTable.Id = DB.OrderTable.Count() + 1;
            orderTable.DescriptionApplication = description;
            orderTable.Id_OrderType = type;
            /*if (type == 1) orderTable.Id_OrderStatus = 1;
            if (type == 2) orderTable.Id_OrderStatus = 2;*/
            orderTable.Id_OrderStatus = 1;
            orderTable.Id_Substation = Id_Sub;
            orderTable.Id_Worker = id_work;
            orderTable.Id_TermApplication = 6;
            orderTable.DateApplication = DateTime.Now;
            orderTable.StartDate = startDate;
            orderTable.EndDate = end;
            orderTable.WorkerOne = worker_one;
            orderTable.WorkerTwo = worker_two;
            orderTable.WorkerThree = worker_three;
            orderTable.Color = "#ffffff";




            using (StoreContext db = new StoreContext())
            {
                //var order = db.OrderTable.FirstOrDefault(s => s.Id == db.OrderTable.Count() + 1);
                //if (order != null) return "Такая заявка уже существует!";
                //SqlConnection sql = new SqlConnection(DB);
                db.OrderTable.Add(orderTable);
                db.SaveChanges();
                return Convert.ToString(orderTable.Id);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderTable"></param>
        /// <param name="id"></param>
        /// <param name="startDate"></param>
        /// <param name="end"></param>
        /// <param name="description"></param>
        /// <param name="worker_one"></param>
        /// <param name="worker_two"></param>
        /// <param name="worker_three"></param>
        /// <returns></returns>
        public static string ChangeOrder(this OrderTable orderTable, int id, DateTime startDate, DateTime end, string description, int worker_one, int worker_two, int worker_three)
        {
            StoreContext DB = new StoreContext();
            orderTable = new StoreContext().OrderTable.FirstOrDefault(s => s.Id == id);
            OrderTablePermission orderTablePermission = new OrderTablePermission();
            orderTablePermission = new StoreContext().OrderTablePermission.FirstOrDefault(s => s.IdOrder == id);
            if (startDate != Convert.ToDateTime("01/01/0001 00:00:00"))
                orderTable.StartDate = startDate;
            if (end != Convert.ToDateTime("01/01/0001 00:00:00"))
                orderTable.EndDate = end;
            if (description != null)
                orderTable.DescriptionApplication = description;
            if (worker_one != 0)
                orderTable.WorkerOne = worker_one;
            if (worker_two != 0)
                orderTable.WorkerTwo = worker_two;
            if (worker_three != 0)
                orderTable.WorkerThree = worker_three;
            orderTable.Id_OrderStatus = 1;
            orderTablePermission.DispatcherComment = null;
            orderTablePermission.Date = DateTime.Now;
            orderTablePermission.Notification = 2;

            using (StoreContext db = new StoreContext())
            {
                db.OrderTable.Update(orderTable);
                db.OrderTablePermission.Update(orderTablePermission); //надо проверить на работоспособность
                db.SaveChanges();
                return "ok";
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderTable"></param>
        /// <returns></returns>
        public static string CheckOrderTable(this OrderTable orderTable)
        {
            if (orderTable == null) return "Ошибка: Не удалось загрузить Заявку";
            StoreContext DB = new StoreContext();

            if (DB.OrderTable == null || DB.OrderTablePermission == null)
            {
                return "Ошибка";
            }

            //orderTable.Id_OrderStatus = 6;
            foreach (var i in DB.OrderTablePermission)
            {
                foreach (var j in DB.OrderTable)
                {
                    if (i.IdOrder == j.Id)
                    {
                        if (i.Term.AddDays(3) < DateTime.Now && j.Id_OrderStatus == 2)
                        {
                            using (StoreContext db = new StoreContext())
                            {
                                db.OrderTablePermission.Remove(i);
                                db.SaveChanges();
                                db.OrderTable.Remove(j);
                                db.SaveChanges();
                            }
                        }
                        if (i.Term.AddDays(3) < DateTime.Now && j.Id_OrderStatus == 3)
                        {
                            using (StoreContext db = new StoreContext())
                            {
                                db.OrderTablePermission.Remove(i);
                                db.SaveChanges();
                                db.OrderTable.Remove(j);
                                db.SaveChanges();
                            }
                        }
                        if (i.Term < DateTime.Now && j.Id_OrderStatus == 4)
                        {
                            using (StoreContext db = new StoreContext())
                            {
                                j.Id_OrderStatus = 2;
                                db.OrderTable.Update(j);
                                db.SaveChanges();
                            }
                        }
                        if (j.Id_OrderStatus == 7 && i.Term.AddDays(3) < DateTime.Now)
                        {
                            FinishOrder(j, j.Id);
                            using (StoreContext db = new StoreContext())
                            {
                                db.OrderTablePermission.Remove(i);
                                db.SaveChanges();
                                db.OrderTable.Remove(j);
                                db.SaveChanges();
                            }
                        }

                    }
                }
            }
            return "";
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderTable"></param>
        /// <returns></returns>
        public static string ChecOrderTable(this OrderTable orderTable)
        {
            if (orderTable == null) return "Ошибка: Не удалось загрузить Заявку";
            StoreContext DB = new StoreContext();

            if(DB.OrderTable == null || DB.OrderTablePermission == null)
            {
                return "Ошибка";
            }

            //orderTable.Id_OrderStatus = 6;
            foreach (var i in DB.OrderTablePermission)
            {
                foreach (var j in DB.OrderTable)
                {
                    if (i.IdOrder == j.Id)
                    {
                        
                        if (j.Id_OrderStatus == 7 && i.Term.AddDays(3) < DateTime.Now)
                        {
                            FinishOrder(j,j.Id);
                        }

                        if (j.Id_OrderStatus == 5 && i.Term.AddDays(3) < DateTime.Now)
                        {
                            //FinishOrder(j, j.Id);
                            using (StoreContext db = new StoreContext())
                            {
                                db.OrderTablePermission.Remove(i);
                                db.SaveChanges();
                                db.OrderTable.Remove(j);
                                db.SaveChanges();
                            }
                        }

                        if (i.Term < DateTime.Now && j.Id_OrderStatus == 4)
                        {
                            using (StoreContext db = new StoreContext())
                            {
                                j.Id_OrderStatus = 6;
                                db.OrderTable.Update(j);
                                db.SaveChanges();
                            }
                        }
                        if (i.Term.AddDays(1) < DateTime.Now && j.Id_OrderStatus == 3)
                        {
                            using (StoreContext db = new StoreContext())
                            {
                                db.OrderTablePermission.Remove(i);
                                db.SaveChanges();
                                db.OrderTable.Remove(j);
                                db.SaveChanges();
                            }
                        }
                        if (i.Term < DateTime.Now && j.Id_OrderStatus == 6)
                        {
                            using (StoreContext db = new StoreContext())
                            {
                                db.OrderTablePermission.Remove(i);
                                db.SaveChanges();
                                db.OrderTable.Remove(j);
                                db.SaveChanges();
                            }
                        }
                        //if ((j.Id_OrderStatus == 2 || j.Id_OrderStatus == 6) && i.Notification == 1)
                        //{
                        //    using (StoreContext db = new StoreContext())
                        //    {
                        //        i.Notification = 0;
                        //        db.OrderTablePermission.Update(i);
                        //        db.SaveChanges();
                        //    }
                        //}
                        /*if (j.EndDate < DateTime.Now && (j.Id_OrderStatus == 2 || j.Id_OrderStatus == 7))
                        {
                            using (StoreContext db = new StoreContext())
                            {
                                archiveorder.DescriptionApplication = j.DescriptionApplication;
                                archiveorder.DateApplication = j.DateApplication;
                                archiveorder.StartDate = j.StartDate;
                                archiveorder.EndDate = j.EndDate;
                                archiveorder.Id_OrderType = j.Id_OrderType;
                                archiveorder.Id_OrderStatus = 7;
                                archiveorder.Id_Substation = j.Id_Substation;
                                archiveorder.Id_Worker = j.Id_Worker;
                                archiveorder.WorkerOne = j.WorkerOne;
                                archiveorder.WorkerTwo = j.WorkerTwo;
                                archiveorder.WorkerThree = j.WorkerThree;
                                db.ArchiveOrder.Add(archiveorder);
                                db.OrderTablePermission.Remove(i);
                                db.OrderTable.Remove(j);
                                //j.Id_OrderStatus = 7;
                                //db.OrderTable.Update(j);
                                db.SaveChanges();
                            }
                        }*/
                    }
                }
            }
            return "Заявка успешно отправлено!";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="archiveOrder"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string AddInArchive(this ArchiveOrder archiveOrder, int id)
        {
            StoreContext DB = new StoreContext();
            AlertTable alertTable = new AlertTable();
            OrderTable orderTable = DB.OrderTable.FirstOrDefault(s => s.Id == id);
            OrderTablePermission orderTablePermission = DB.OrderTablePermission.FirstOrDefault(s => s.IdOrder == id);
            if (orderTable == null) return "Ошибка: Не удалось загрузить!";
            archiveOrder.DescriptionApplication = orderTable.DescriptionApplication;
            archiveOrder.DateApplication = orderTable.DateApplication;
            archiveOrder.StartDate = orderTable.StartDate;
            archiveOrder.EndDate = orderTable.EndDate;
            archiveOrder.Id_OrderType = orderTable.Id_OrderType;
            archiveOrder.Id_OrderStatus = 7;
            archiveOrder.Id_Substation = orderTable.Id_Substation;
            archiveOrder.Id_Worker = orderTable.Id_Worker;
            archiveOrder.WorkerOne = orderTable.WorkerOne;
            archiveOrder.WorkerTwo = orderTable.WorkerTwo;
            archiveOrder.WorkerThree = orderTable.WorkerThree;
            foreach (var item in new StoreContext().Users)
            {
                if (item.RoleId == 15 && item.Id == orderTable.Id_Worker)
                {
                    //alertTable.AddAlert("Заявка перенесена в архив", 0, item.Id, "/ArchiveOrderPage", id);
                }
            }
            using (StoreContext db = new StoreContext())
            {
                db.ArchiveOrder.Add(archiveOrder);
                db.SaveChanges();
                db.OrderTablePermission.Remove(orderTablePermission);
                db.SaveChanges();
                db.OrderTable.Remove(orderTable);
                db.SaveChanges();
            }
            return "ok";
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderTable"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string DeleteOrder(this OrderTable orderTable, int id)
        {
            if (orderTable == null) return "Ошибка: Не удалось загрузить Заявку";
            StoreContext DB = new StoreContext();

            
            using (StoreContext db = new StoreContext())
            {
                db.OrderTablePermission.Remove(db.OrderTablePermission.FirstOrDefault(s => s.IdOrder == id));
                db.SaveChanges();
                db.OrderTable.Remove(db.OrderTable.FirstOrDefault(s => s.Id == id));
                db.SaveChanges();
            }
            
            return "Заявка успешно отправлено!";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderTable"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string ReplacementOrder(this OrderTable orderTable, int id)
        {
            if (orderTable == null) return "Ошибка: Не удалось загрузить Заявку!";
            orderTable.Id_OrderStatus = 7;
            using (StoreContext db = new StoreContext())
            {
                db.OrderTable.Update(orderTable);
                db.SaveChanges();
            }
            return "Статус изменен!";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orderTable"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string FinishOrder(this OrderTable orderTable, int id)
        {
            if (orderTable == null) return "Ошибка: Не удалось загрузить Заявку!";
            //Добавление в Архив
            ArchiveOrder archiveOrder = new ArchiveOrder();
            archiveOrder.AddInArchive(id);
            return "Заявка отправлена в Aрхив!";
        }

        #endregion

        #region Диспетчер

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dutyScheduleTable"></param>
        /// <returns></returns>
        public static string DeleteDuty(this DutyScheduleTable dutyScheduleTable)
        {
            if (dutyScheduleTable == null) return "Ошибка";
            using(StoreContext db = new StoreContext())
            {
                db.DutyScheduleTable.Remove(dutyScheduleTable);
                db.SaveChanges();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dutyScheduleTable"></param>
        /// <param name="id_disp"></param>
        /// <param name="sDate"></param>
        /// <param name="eDate"></param>
        /// <param name="color"></param>
        /// <returns></returns>
        public static string AddDuty(this DutyScheduleTable dutyScheduleTable, int id_disp, DateTime sDate, DateTime eDate, string color)
        {
            if (dutyScheduleTable == null) return "Ошибка";
            dutyScheduleTable.Id_dispatcher = id_disp;
            dutyScheduleTable.StartDate = sDate;
            dutyScheduleTable.EndDate = eDate;
            dutyScheduleTable.Color = color;

            using(StoreContext db = new StoreContext())
            {
                db.DutyScheduleTable.Add(dutyScheduleTable);
                db.SaveChanges();
            }
            return "";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shiftSchedule"></param>
        /// <param name="id_user"></param>
        /// <returns></returns>
        public static string PassShift(this ShiftSchedule shiftSchedule, int id_user)
        {
            try
            {
                if (shiftSchedule == null) return "Не удалось загрузить таблицу";

                //if (shiftSchedule.Id != id_user) return "Не удалось загрузить таблицу";
                shiftSchedule.EndDate = DateTime.Now;
                //shiftSchedule.Comment = comment;
                using (StoreContext db = new StoreContext())
                {
                    db.ShiftSchedule.Update(shiftSchedule);
                    db.SaveChanges();
                }
                return "";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="shiftSchedule"></param>
        /// <param name="id_user"></param>
        /// <returns></returns>
        public static string AcceptShift(this ShiftSchedule shiftSchedule, int id_user)
        {
            try
            {
                if (shiftSchedule == null) return "Не удалось загрузить таблицу";
                shiftSchedule.StartDate = DateTime.Now;
                shiftSchedule.Id_Dispatcher = id_user;
                //shiftSchedule.Comment = comment;
                using (StoreContext db = new StoreContext())
                {
                    db.ShiftSchedule.Add(shiftSchedule);
                    db.SaveChanges();
                }
                return "Смена принято";
            }
            catch (Exception ex) { return ex.Message; }
        }

        #endregion

        #region Оповещение

        /// <summary>
        /// 
        /// </summary>
        /// <param name="allAlertTable"></param>
        /// <param name="comment"></param>
        /// <param name="fromuser"></param>
        /// <param name="whichuser"></param>
        /// <returns></returns>
        public static string AddAllAlert(this AllAlertTable allAlertTable, string comment, int fromuser, int whichuser)
        {
            if (allAlertTable == null) return "Ошибка: Не удалось загрузить Заявку";
            allAlertTable.Comment = comment;
            allAlertTable.Date = DateTime.Now;
            allAlertTable.FromUser = fromuser;
            allAlertTable.WhichUser = whichuser;
            using (StoreContext db = new StoreContext())
            {
                db.AllAlertTable.Add(allAlertTable);
                db.SaveChanges();
                return "Ok";
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alertTable"></param>
        /// <param name="comment"></param>
        /// <param name="fromuser"></param>
        /// <param name="whichuser"></param>
        /// <param name="link"></param>
        /// /// <param name="itemId"></param>
        /// <returns></returns>
        public static string AddAlert(this AlertTable alertTable, string comment, int fromuser, int whichuser, string link, int itemId)
        {
            if (alertTable == null) return "Ошибка: Не удалось загрузить Заявку";

            StoreContext DB = new StoreContext();
            //AllAlertTable allAlertTable = new AllAlertTable();
            //allAlertTable.AddAllAlert(comment, fromuser, whichuser);
            //int id = 0;
            //if (DB.AlertTable.Count() == 0)
            //    id = 1;
            //else
            //    id = DB.AlertTable.LastOrDefault().Id + 1;


            //alertTable.Id = id;
            alertTable.Comment = comment;
            alertTable.Date = DateTime.Now;
            alertTable.FromUser = fromuser;
            alertTable.WhichUser = whichuser;
            alertTable.Link = link;
            alertTable.IdOrder = itemId;


            using (StoreContext db = new StoreContext())
            {
                db.AlertTable.Add(alertTable);
                db.SaveChanges();
                return Convert.ToString(alertTable.Id);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alertTable"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public static string DeleteAlert(this AlertTable alertTable, int id)
        {
            StoreContext DB = new StoreContext();
            if (alertTable == null) return "Не удалось загрузить таблицу";
            alertTable = DB.AlertTable.FirstOrDefault(s => s.WhichUser == id);
            using (StoreContext db = new StoreContext())
            {
                db.AlertTable.Remove(alertTable);
                db.SaveChanges();
            }
            return "";
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="alertTable"></param>
        /// <returns></returns>
        public static string DeleteAlert(this AlertTable alertTable)
        {
            StoreContext DB = new StoreContext();
            if (alertTable == null) return "Не удалось загрузить таблицу";
            using (StoreContext db = new StoreContext())
            {
                db.AlertTable.Remove(alertTable);
                db.SaveChanges();
            }
            return "";
        }
        #endregion

        #region Чат

        /// <summary>
        /// 
        /// </summary>
        /// <param name="chatTable"></param>
        /// <param name="id"></param>
        /// <param name="text"></param>
        /// <param name="idToUser"></param>
        /// <returns></returns>
        public static string SendMessage(this ChatTable chatTable, int id, string text, int idToUser)
        {
            if (chatTable == null) return "Ошибка: Не удалось загрузить таблицу";
            StoreContext DB = new StoreContext();

            chatTable.Text = text;
            chatTable.StartDate = DateTime.Now;
            chatTable.FromUser = id;
            //chatTable.ToUser = idToUser;
            using (StoreContext db = new StoreContext())
            {
                db.ChatTable.Add(chatTable);
                db.SaveChanges();
            }

            return "Сообщение отправленно";
        }

        #endregion

        #region Новая база заявок 

        /// <summary>
        /// 
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static string CreateApplication(this ApplicationTable table)
        {
            try
            {
                SwitchFormTable switchForm = new SwitchFormTable();
                using (StoreContext db = new StoreContext())
                {
                    db.ApplicationTable.Add(table);
                    db.SaveChanges();
                    
                }
                switchForm.idApp = table.Id;
                switchForm.CreateSwitchForm();
                return "Заявка создана!";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="switchForm"></param>
        /// <returns></returns>
        public static string CreateSwitchForm(this SwitchFormTable switchForm)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.SwitchFormTable.Add(switchForm);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }

        }

        /// <summary>
        /// Ассинхронное изменение БП
        /// </summary>
        /// <param name="switchForm"></param>
        /// <returns></returns>
        public static void EditSwitchForm(this SwitchFormTable switchForm)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.SwitchFormTable.Update(switchForm);
                    db.SaveChanges();
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="switchForm"></param>
        /// <returns></returns>
        public static string DelSwitchForm(this SwitchFormTable switchForm)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.SwitchFormTable.Remove(switchForm);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static string AddContent(this ContentOfOperations content)
        {
            try
            {
                using(StoreContext db = new StoreContext())
                {
                    db.ContentOfOperations.Add(content);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static string DelContent(this ContentOfOperations content)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.ContentOfOperations.Remove(content);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// Изменение значений контента
        /// </summary>
        /// <param name="content"></param>
        /// <returns></returns>
        public static async Task EditContentAsync(this ContentOfOperations content)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.ContentOfOperations.Update(content);
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="signature"></param>
        /// <returns></returns>
        public static string AddSwitchSig(this SwitchFormSignature signature)
        {
            try
            {
                using(StoreContext db = new StoreContext())
                {
                    db.SwitchFormSignature.Add(signature);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static string DeleteApplication(this ApplicationTable table)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.ApplicationTable.Remove(table);
                    db.SaveChanges();
                }
                return "Заявка удалена!";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="table"></param>
        /// <returns></returns>
        public static string EditApplication(this ApplicationTable table)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.ApplicationTable.Update(table);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="signature"></param>
        /// <returns></returns>
        public static string AddSignature(this AppSignature signature)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.AppSignature.Add(signature);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="signature"></param>
        /// <returns></returns>
        public static string AppCheckDisp(this AppCheckDisp signature)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.AppCheckDisp.Add(signature);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="signature"></param>
        /// <returns></returns>
        public static string AppOpenDisp(this AppOpenDisp signature)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.AppOpenDisp.Add(signature);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="signature"></param>
        /// <returns></returns>
        public static string AppCloseDisp(this AppCloseDisp signature)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.AppCloseDisp.Add(signature);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        #endregion

        #region новое оповещение 


        /// <summary>
        /// 
        /// </summary>
        /// <param name="notification"></param>
        /// <param name="userId"></param>
        /// <param name="typeId"></param>
        /// <param name="idApp"></param>
        /// <param name="whirchId"></param>
        public static async Task CreateNotification(this NotificationTable notification, int userId, int typeId, int idApp, int whirchId)
        {
            try
            {
                NotificationTable table = new NotificationTable();
                table.Date = DateTime.Now;
                table.FromId = userId;
                table.Id_Type = typeId;
                table.Id_App = idApp;
                table.WhitchId = whirchId;
                using (StoreContext db = new StoreContext())
                {
                    await db.NotificationTable.AddAsync(table);
                    await db.SaveChangesAsync();
                }
            }
            catch { }
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="notification"></param>
        public static async Task DeleteNotification(this NotificationTable notification)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.NotificationTable.Remove(notification);
                    await db.SaveChangesAsync();
                }
            }
            catch { }
        }

        /// <summary>
        /// Создание бегущей строки
        /// </summary>
        /// <param name="marquee"></param>
        /// <returns></returns>
        public static string AddMarquee(this MarqueeTable marquee)
        {
            try
            {
                using(StoreContext db = new StoreContext())
                {
                    db.MarqueeTable.Add(marquee);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// Удаление бегущей строки
        /// </summary>
        /// <param name="marquee"></param>
        /// <returns></returns>
        public static string DelMarquee(this MarqueeTable marquee)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.MarqueeTable.Remove(marquee);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// Создание телефонограммы
        /// </summary>
        /// <param name="phonegramTable"></param>
        /// <returns></returns>
        public static string CreatePhonegram(this PhonegramTable phonegramTable)
        {
            try
            {
                using(StoreContext db = new StoreContext())
                {
                    db.PhonegramTable.Add(phonegramTable);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch(Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Удаление телефонограммы
        /// </summary>
        /// <param name="phonegramTable"></param>
        /// <returns></returns>
        public static string DelPhonegram(this PhonegramTable phonegramTable)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.PhonegramTable.Remove(phonegramTable);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        #endregion

        #region Оперативный журнал

        /// <summary>
        /// Метод добавление записей в оперативный журнал
        /// </summary>202
        /// <param name="operational"></param>
        /// <returns></returns>
        public static string AddComment(this OperationalLog operational)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.OperationalLog.Add(operational);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
            
        }

        /// <summary>
        /// Асинхронный метод добавление записей в оперативный журнал
        /// </summary>
        /// <param name="operational"></param>
        /// <returns></returns>
        public static async Task<string> AddCommentAsync(this OperationalLog operational)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    await db.OperationalLog.AddAsync(operational);
                    await db.SaveChangesAsync();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }

        }

        /// <summary>
        /// Метод добавление визы/замечания в оперативный журнал
        /// </summary>
        /// <param name="operational"></param>
        /// <returns></returns>
        public static string AddVisa(this OperationalLog operational)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.OperationalLog.Update(operational);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
            
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="operational"></param>
        /// <returns></returns>
        public static string DelComment(this OperationalLog operational)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.OperationalLog.Remove(operational);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
            
        }

        /// <summary>
        /// Асинхронный метод добавление записей в Веб-Архив
        /// </summary>
        /// <param name="eventHistory"></param>
        public static async Task AddEventHistoryAsync(this EventHistory eventHistory)
        {
            using(StoreContext db = new StoreContext())
            {
                await db.EventHistory.AddAsync(eventHistory);
                await db.SaveChangesAsync();
            }
        }

        #endregion

          #region График дежурств

        /// <summary>
        /// Добавить График Дежурств
        /// </summary>
        /// <param name="excelFile"></param>
        /// <returns></returns>
        public static string AddExFile(this ExcelFileTable excelFile)
        {
            try
            {
                using(StoreContext db = new StoreContext())
                {
                    db.ExcelFileTable.Add(excelFile);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch(Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// Изменить График Дежурств
        /// </summary>
        /// <param name="excelFile"></param>
        /// <returns></returns>
        public static string EditExFile(this ExcelFileTable excelFile)
        {
            try
            {
                using(StoreContext db = new StoreContext())
                {
                    db.ExcelFileTable.Update(excelFile);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch(Exception ex)
            {
                return ex.Message;
            }
        }

        #endregion

        #region События

        /// <summary>
        /// Добавление события в календаре
        /// </summary>
        /// <param name="eventTable"></param>
        /// <returns></returns>
        public static string AddEvent(this EventTable eventTable)
        {
            try
            {
                using(StoreContext db = new StoreContext())
                {
                    db.EventTable.Add(eventTable);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }

        }

        /// <summary>
        /// Удаление события в календаре
        /// </summary>
        /// <param name="eventTable"></param>
        /// <returns></returns>
        public static string DelEvent(this EventTable eventTable)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.EventTable.Remove(eventTable);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }

        }



        #endregion

        #region Документы

        /// <summary>
        /// Добавление документа
        /// </summary>
        /// <param name="documents"></param>
        /// <returns></returns>
        public static string AddDoc(this Documents documents)
        {
            try
            {
                using(StoreContext db= new StoreContext())
                {
                    db.Documents.Add(documents);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// Удаление документа
        /// </summary>
        /// <param name="documents"></param>
        /// <returns></returns>
        public static string DelDoc(this Documents documents)
        {
            try
            {
                using (StoreContext db = new StoreContext())
                {
                    db.Documents.Remove(documents);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>
        /// Добавление оперативной схемы
        /// </summary>
        /// <param name="operationalScheme"></param>
        /// <returns></returns>
        public static string AddOperationalScheme(this OperationalScheme operationalScheme)
        {
            try
            {
                using(StoreContext db = new StoreContext())
                {
                    db.OperationalScheme.Add(operationalScheme);
                    db.SaveChanges();
                }
                return "ok";
            }
            catch(Exception ex) { return ex.Message; }
        }
        #endregion
    }
}
