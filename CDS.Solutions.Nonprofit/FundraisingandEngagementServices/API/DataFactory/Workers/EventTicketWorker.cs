﻿using System;
using System.Linq;
using FundraisingandEngagement.Data;
using FundraisingandEngagement.Models.Entities;

namespace FundraisingandEngagement.DataFactory.Workers
{
    public class EventTicketWorker : IFactoryFloor<EventTicket>
    {
        private PaymentContext DataContext;

        public EventTicketWorker(PaymentContext context)
        {
            DataContext = context;
        }

        public EventTicket GetById(Guid eventTicketId)
        {
            return DataContext.EventTicket.FirstOrDefault(t => t.EvenTicketId == eventTicketId);
        }



        public int UpdateCreate(EventTicket eventTicket)
        {

            if (Exists(eventTicket.EvenTicketId))
            {

                DataContext.EventTicket.Update(eventTicket);
                return DataContext.SaveChanges();
            }
            else if (eventTicket != null)
            {
                eventTicket.CreatedOn = DateTime.Now;
                DataContext.EventTicket.Add(eventTicket);

                return DataContext.SaveChanges();
            }
            else
            {
                return 0;
            }
        }

        public int Delete(Guid guid)
        {
            EventTicket existingRecord = GetById(guid);
            if (existingRecord != null)
            {
                existingRecord.Deleted = true;
                existingRecord.DeletedDate = DateTime.Now;

                DataContext.Update(existingRecord);
                return DataContext.SaveChanges();
            }
            else
            {
                return 0;
            }
        }

        public bool Exists(Guid guid)
        {
            return DataContext.EventTicket.Any(x => x.EvenTicketId == guid);
        }
    }
}