﻿using System;
using System.Linq;
using FundraisingandEngagement.Data;
using FundraisingandEngagement.Models.Entities;

namespace FundraisingandEngagement.DataFactory.Workers
{
    public class MembershipWorker : IFactoryFloor<Membership>
    {
        private PaymentContext DataContext;

        public MembershipWorker(PaymentContext context)
        {
            DataContext = context;
        }

        public Membership GetById(Guid recordID)
        {
            return DataContext.Membership.FirstOrDefault(c => c.MembershipId == recordID);
        }



        public int UpdateCreate(Membership updateRecord)
        {

            if (Exists(updateRecord.MembershipId))
            {

                updateRecord.SyncDate = DateTime.Now;

                DataContext.Membership.Update(updateRecord);
                return DataContext.SaveChanges();
            }
            else if (updateRecord != null)
            {
                updateRecord.CreatedOn = DateTime.Now;
                DataContext.Membership.Add(updateRecord);
                return DataContext.SaveChanges();
            }
            else
            {
                return 0;
            }
        }

        public int Delete(Guid guid)
        {
            Membership existingRecord = GetById(guid);
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
            return DataContext.Membership.Any(x => x.MembershipId == guid);
        }
    }
}