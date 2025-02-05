﻿using System;
using System.Linq;
using FundraisingandEngagement.Data;
using FundraisingandEngagement.Models.Entities;

namespace FundraisingandEngagement.DataFactory
{
    public class GiftAidDeclarationWorker : IFactoryFloor<GiftAidDeclaration>
    {
        private PaymentContext DataContext;


        public GiftAidDeclarationWorker(PaymentContext context)
        {
            DataContext = context;
        }

        public GiftAidDeclaration GetById(Guid GiftAidDeclarationId)
        {
            return DataContext.GiftAidDeclaration.FirstOrDefault(t => t.GiftAidDeclarationId == GiftAidDeclarationId);
        }


        public int UpdateCreate(GiftAidDeclaration updateRecord)
        {
            if (Exists(updateRecord.GiftAidDeclarationId))
            {
                updateRecord.SyncDate = DateTime.Now;

                DataContext.GiftAidDeclaration.Update(updateRecord);
                return DataContext.SaveChanges();
            }
            else if (updateRecord != null)
            {
                updateRecord.CreatedOn = DateTime.Now;
                DataContext.GiftAidDeclaration.Add(updateRecord);
                return DataContext.SaveChanges();
            }
            else
            {
                return 0;
            }
        }

        public int Delete(Guid guid)
        {
            GiftAidDeclaration existingRecord = GetById(guid);
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
            return DataContext.GiftAidDeclaration.Any(x => x.GiftAidDeclarationId == guid);
        }

    }
}