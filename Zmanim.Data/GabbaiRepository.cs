﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Zmanim.TimeZone;
using Zmanim.TzDatebase;
using Zmanim.Utilities;

namespace Zmanim.Data
{
    public class GabbaiRepository
    {
        private string _connectionString;

        public GabbaiRepository(string cn)
        {
            _connectionString = cn;
        }


        public void UpdateEvent(int eventId, Event e)
        {
            using (var context = new MyShulWorldDBDataContext(_connectionString))
            {
                Event a = context.Events.FirstOrDefault(i => i.Id == eventId);
                a.EventName = e.EventName;
                a.EventTypeId = e.EventTypeId;
                a.Date = e.Date;
                a.Time = e.Time;
                context.SubmitChanges();
            }
        }

        public void UpdateEventType(int eventTypeId, EventType et)
        {
            using (var context = new MyShulWorldDBDataContext(_connectionString))
            {
                EventType eventType = context.EventTypes.FirstOrDefault(e => e.ID == eventTypeId);
                if (eventType != null)
                {
                    eventType.Name = et.Name;
                    eventType.Fixed = et.Fixed;
                    eventType.FixedTime = et.FixedTime;
                    eventType.BasedOn = et.BasedOn;
                    eventType.TimeDifference = et.TimeDifference;
                    eventType.StartDate = et.StartDate;
                    eventType.EndDate = et.EndDate;
                    eventType.YearsPopulated = et.YearsPopulated;
                    eventType.TypesOfDaysApplicable = et.TypesOfDaysApplicable;
                    eventType.TypesOfDaysExcluded = et.TypesOfDaysExcluded;
                    eventType.DaysOfWeekApplicable = et.DaysOfWeekApplicable;
                }

                context.SubmitChanges();
                var events = context.Events.Where(e => e.EventTypeId == eventTypeId && e.Date > DateTime.Now);
                context.Events.DeleteAllOnSubmit(events);
            }
        }

        public void DeleteEvent(int eventId)
        {
            using (var context = new MyShulWorldDBDataContext(_connectionString))
            {
                context.Events.DeleteOnSubmit(GetEventById(eventId));
                context.SubmitChanges();
            }
        }

        public void DeleteEventType(int eventTypeId)
        {
            using (var context = new MyShulWorldDBDataContext(_connectionString))
            {
                context.EventTypes.DeleteOnSubmit(GetEventTypeById(eventTypeId));
                var events = context.Events.Where(e => e.EventTypeId == eventTypeId && e.Date > DateTime.Now);
                context.Events.DeleteAllOnSubmit(events);
                context.SubmitChanges();
            }
        }

        private EventType GetEventTypeById(int eventTypeId)
        {
            using (var context = new MyShulWorldDBDataContext(_connectionString))
            {
                return context.EventTypes.FirstOrDefault(et => et.ID == eventTypeId);
            }
        }

        public void AddEventType(EventType eventType)
        {
            if (eventType.StartDate == null)
            {
                eventType.StartDate = DateTime.Now;
            }
            using (var context = new MyShulWorldDBDataContext(_connectionString))
            {
                context.EventTypes.InsertOnSubmit(eventType);
                context.SubmitChanges();
            }
        }

        public void AddEvent(Event e)
        {
            using (var context = new MyShulWorldDBDataContext(_connectionString))
            {
                context.Events.InsertOnSubmit(e);
                context.SubmitChanges();
            }
        }

        public string GetTimeBasedOnSomething(DateTime date, int? basedOn, double minutesDifference)
        {
            string locationName = "Lakewood, NJ";
            double latitude = 40.09596; //Lakewood, NJ
            double longitude = -74.22213; //Lakewood, NJ
            double elevation = 0; //optional elevation
            ITimeZone timeZone = new OlsonTimeZone("America/New_York");
            GeoLocation location = new GeoLocation(locationName, latitude, longitude, elevation, timeZone);
            DateTime dateTime = new DateTime();
            if (basedOn == (int)BasedOn.Sunset)
            {
                ComplexZmanimCalendar zc = new ComplexZmanimCalendar(date, location);

                if (zc.GetSunset() != null)
                {
                    dateTime = (DateTime)zc.GetSunset();
                }
            }
            return dateTime.AddMinutes(minutesDifference).ToShortTimeString();
        }

        public void PopulateEventsForAYear(string year, IEnumerable<EventType> eventTypes)
        {
            foreach (EventType et in eventTypes)
            {
                if (et.YearsPopulated.Contains(year))
                {
                    continue;
                }
                GenerateInsertionOfEventsForYear(year, et);
            }
        }

        private void GenerateInsertionOfEventsForYear(string year, EventType eventType)
        {
            HEBCALItems items = new HEBCALItems
            {
                Items = (Item[])GetItemsForYear(year)
            };
            
            for (DateTime x = DateTime.Parse(year + "/01/01"); x < DateTime.Parse(year + "/12/31"); x.AddDays(1))
            {
                if (IsApplicable(x, items.Items.FirstOrDefault(i => DateTime.Parse(i.Date) == x), eventType))
                {
                    Event e = new Event
                    {
                        Date = x,
                        EventName = eventType.Name,
                        EventTypeId = eventType.ID,
                    };
                    //may be more performent to wrap this whole "for" loop (beginning line 97) in the following "if" statement and to repeat all the
                    //logic again in the following "else if" cuz then only has to check once
                    if (eventType.FixedTime != null)
                    {
                        e.Time = eventType.FixedTime;
                    }
                    else if (eventType.BasedOn != null)
                    {
                        double minutes = eventType.TimeDifference.Value;
                        e.Time = GetTimeBasedOnSomething(x, eventType.BasedOn, minutes);
                    }
                    AddEvent(e);
                }
            }
            using (var context = new MyShulWorldDBDataContext(_connectionString))
            {
                //add the year to eventType.YearsPopulated
                context.EventTypes.FirstOrDefault(e => e.ID == eventType.ID).YearsPopulated += year + ",";
                context.SubmitChanges();
            }
        }

        private string GetTypeOfDay(Item ifd)
        {
            if (ifd.YomTov && !ifd.Title.Contains("Rosh Hashana") && !ifd.Title.Contains("Yom Kippur"))
            {
                return "YomTov";
            }
            if (ifd.Category == "roshchodesh")
            {
                return "RoshChodesh";
            }
            if (ifd.Title.Contains("CH''M"))
            {
                return "CholHamoed";
            }
            if (ifd.Title == "Yom Kippur")
            {
                return "YomKippur";
            }
            if (ifd.Title == "Rosh Hashana")
            {
                return "RoshHashana";
            }
            if (ifd.Title == "Tish'a B'av")
            {
                return "TishaB'av";
            }
            //might not be so efficient
            if (ifd.Memo.Contains("fast") && ifd.Title != "Yom Kippur" && ifd.Title != "Tish'a B'av" && ifd.Memo != "Fast of the First Born")
            {
                return "Taanis";
            }
            return "RegularDay";
        }

        private IEnumerable<string> GetTypesOfDay(Item ifd)
        {
            List<string> types = new List<string>();
            if (ifd.YomTov && !ifd.Title.Contains("Rosh Hashana") && !ifd.Title.Contains("Yom Kippur"))
            {
                types.Add("YomTov"); 
            }
            if (ifd.Category == "roshchodesh")
            {
                types.Add("RoshChodesh");
            }
            if (ifd.Title.Contains("CH''M"))
            {
                types.Add("CholHamoed");
            }
            if (ifd.Title == "Yom Kippur")
            {
                types.Add("YomKippur");
            }
            if (ifd.Title == "Rosh Hashana")
            {
                types.Add("RoshHashana");
            }
            if (ifd.Title == "Tish'a B'av")
            {
                types.Add("TishaB'av");
            }
            //might not be so efficient
            if (ifd.Memo.Contains("fast") && ifd.Title != "Yom Kippur" && ifd.Title != "Tish'a B'av" && ifd.Memo != "Fast of the First Born")
            {
                types.Add("Taanis");
            }
            return types;
        }

        private bool IsApplicable(DateTime date, Item itemForDate, EventType et)
        {
            if ((et.StartDate != null && date < et.StartDate) || (et.EndDate != null && date > et.EndDate))
            {
                return false;
            }

            if (itemForDate != null)
            {
                if (et.TypesOfDaysApplicable.Contains(GetTypeOfDay(itemForDate)))
                {
                    return et.DaysOfWeekApplicable.Contains(date.DayOfWeek.ToString());
                }
            }
            return et.Identifier.Contains(date.DayOfWeek.ToString());
        }

        private IEnumerable<Item> GetItemsForYear(string year)
        {
            using (var webclient = new WebClient())
            {
                string json =
                    webclient.DownloadString("https://www.hebcal.com/hebcal/?cfg=json&v=1&year=" + year +
                                             "&i=off&maj=on&min=on&nx=on&mf=on&ss=on&lg=s");
                return JsonConvert.DeserializeObject<Item[]>(json);
            }
        }


        public Event GetEventById(int eventId)
        {
            using (var context = new MyShulWorldDBDataContext(_connectionString))
            {
                return context.Events.FirstOrDefault(e => e.Id == eventId);
            }
        }

        public IEnumerable<EventType> GetAllEventTypes()
        {
            using (var context = new MyShulWorldDBDataContext(_connectionString))
            {
                return context.EventTypes.ToList();
            }
        }

        public IEnumerable<Event> GetEventsForDay(DateTime date)
        {
            using (var context = new MyShulWorldDBDataContext(_connectionString))
            {
                return context.Events.Where(e => e.Date == date).ToList();
            }
        }

        public void PopulateShkiaFor30Days()
        {
            var date = DateTime.Now;

            string locationName = "Lakewood, NJ";
            double latitude = 40.09596; //Lakewood, NJ
            double longitude = -74.22213; //Lakewood, NJ
            double elevation = 0; //optional elevation
            ITimeZone timeZone = new OlsonTimeZone("America/New_York");
            GeoLocation location = new GeoLocation(locationName, latitude, longitude, elevation, timeZone);
            //ComplexZmanimCalendar zc = new ComplexZmanimCalendar(location);
            //optionally set it to a specific date with a year, month and day
            //ComplexZmanimCalendar zc = new ComplexZmanimCalendar(new DateTime(1969, 2, 8), location);
            using (var context = new MyShulWorldDBDataContext(_connectionString))
            {
                for (int x = 0; x < 30; x++)
                {
                    ComplexZmanimCalendar zc = new ComplexZmanimCalendar(date, location);
                    DateTime dateTime = (DateTime)zc.GetSunset();
                    //TimeSpan time = (TimeSpan)dateTime.ToString("h:mm tt");
                    string time = dateTime.ToShortTimeString();
                    //context.Events.InsertOnSubmit(new Event
                    //{
                    //    EventName="Shkia",
                    //    Time = time,
                    //    Date=date
                    //});
                    AddEvent(new Event
                    {
                        EventName = "Shkia",
                        Time = time,
                        Date = date
                    });
                    date = date.AddDays(1);
                    //context.SubmitChanges();
                }

            }
        }

        //private bool IsApplicable(DateTime date, Item itemForDate, EventType et)
        //{
        //    if ((et.StartDate != null && date < et.StartDate) || (et.EndDate != null && date > et.EndDate))
        //    {
        //        return false;
        //    }
        //    //foreach (Item i in itemsForDate)
        //    if (itemForDate != null)
        //    {

        //        //or do .contains
        //        if (et.Identifier == "YomTov" && (!itemForDate.Title.Contains("Rosh Hashana") && !itemForDate.Title.Contains("Yom Kippur")))
        //        {
        //            return itemForDate.YomTov;
        //        }
        //        if (et.Identifier.Contains("YomKippur"))
        //        {
        //            return itemForDate.Title.Contains("Yom Kippur");
        //        }
        //        if (et.Identifier.Contains("RoshHashana"))
        //        {
        //            return itemForDate.Title.Contains("Rosh Hashana");
        //        }
        //        if (et.Identifier.Contains("ExclusivelyShabbos"))
        //        {
        //            return DateTime.Parse(itemForDate.Date).DayOfWeek == DayOfWeek.Saturday;
        //        }

        //        //special days that are regardless of day of week
        //        if (et.Identifier.Contains("TishaB'av"))
        //        {
        //            return itemForDate.Title == "Tish'a B'av";
        //        }
        //        //add more special days...

        //        if (et.Identifier.Contains("ExclusivelyRoshChodesh"))
        //        {
        //            return itemForDate.Category.Contains("roshchodesh") &&
        //                   et.Identifier.Contains(DateTime.Parse(itemForDate.Date).DayOfWeek.ToString());
        //        }
        //        if (!et.Identifier.Contains("RoshChodesh"))
        //        {
        //            return !itemForDate.Category.Contains("roshchodesh") &&
        //                   et.Identifier.Contains(DateTime.Parse(itemForDate.Date).DayOfWeek.ToString());
        //        }
        //        if (et.Identifier.Contains("ExclusivelyTaanis"))
        //        {
        //            return itemForDate.Memo.Contains("fast") && !itemForDate.Title.Contains("Yom Kippur") && !itemForDate.Title.Contains("Tish'a B'av") &&
        //                   et.Identifier.Contains(DateTime.Parse(itemForDate.Date).DayOfWeek.ToString());
        //        }
        //        if (!et.Identifier.Contains("Taanis"))
        //        {
        //            return !itemForDate.Memo.Contains("fast") && !itemForDate.Title.Contains("Yom Kippur") && !itemForDate.Title.Contains("Tish'a B'av") &&
        //                   et.Identifier.Contains(DateTime.Parse(itemForDate.Date).DayOfWeek.ToString());
        //        }
        //    }
        //    return et.Identifier.Contains(date.DayOfWeek.ToString());
        //    //return identifier.Contains(DateTime.Parse(itemForDate.Date).DayOfWeek.ToString());
        //}

        //Before found better json link:
        //private IEnumerable<Day> GetTypesOfDays(string year)
        //{
        //    using (var webclient = new WebClient())
        //    {
        //        string json = webclient.DownloadString("https://www.hebcal.com/hebcal/?cfg=fc&v=1&start="+year+"-01-01&end="+year+"-12-31&i=off&zip=08701&maj=on&min=on&nx=on&mf=on&ss=on&mod=on&lg=s&s=on");
        //        return JsonConvert.DeserializeObject<Day[]>(json);
        //    }
        //}

        //public void AddEventBasedOnSomething(DateTime date, EventType eventType)
        //{
        //    string locationName = "Lakewood, NJ";
        //    double latitude = 40.09596; //Lakewood, NJ
        //    double longitude = -74.22213; //Lakewood, NJ
        //    double elevation = 0; //optional elevation
        //    ITimeZone timeZone = new OlsonTimeZone("America/New_York");
        //    GeoLocation location = new GeoLocation(locationName, latitude, longitude, elevation, timeZone);
        //    if (eventType.BasedOn != null && eventType.BasedOn == (int)BasedOn.Sunset)
        //    {
        //        ComplexZmanimCalendar zc = new ComplexZmanimCalendar(date, location);
        //        DateTime dateTime = new DateTime();
        //        double diff = eventType.TimeDifference.Value;
        //        if (zc.GetSunset() != null)
        //        {
        //            dateTime = (DateTime)zc.GetSunset();
        //        }
        //        AddEvent(new Event
        //        {
        //            Date = date,
        //            EventName = eventType.Name,
        //            EventTypeId = eventType.ID,
        //            Time = dateTime.AddMinutes(diff).ToShortTimeString()
        //        });
        //    }
        //}

    }
}