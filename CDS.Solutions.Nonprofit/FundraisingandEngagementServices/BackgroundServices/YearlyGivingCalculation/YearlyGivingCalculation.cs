﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FundraisingandEngagement.Data;
using FundraisingandEngagement.Data.DataSynchronization;
using FundraisingandEngagement.Models.Entities;
using FundraisingandEngagement.Models.Enums;
using Microsoft.Extensions.Logging;

namespace FundraisingandEngagement.BackgroundServices
{
    public class YearlyGivingCalculator : IYearlyGivingCalculator
    {
        private readonly PaymentContext _dataContext;
        private readonly ILogger _logger;
        private readonly IDbEntitySynchronizer _dbEntitySynchronizer;

        public YearlyGivingCalculator(PaymentContext dataContext, ILogger logger, IDbEntitySynchronizer dbEntitySynchronizer)
        {
            this._logger = logger;
            this._dbEntitySynchronizer = dbEntitySynchronizer;
            this._dataContext = dataContext;
        }

        public void UpdateCustomer(Guid? customerId, int? customerIdType)
        {
            this._logger.LogInformation($"Entering UpdateCustomer for Customer with Id {customerId} and type {customerIdType}");

            var customer = GetCustomer(customerId, customerIdType);
            if (customer == null)
            {
                this._logger.LogError($"No Customer Found with Id {customerId} and Type {customerIdType}. Exiting");
                return;
            }

            if (customer is Account)
            {
                this._logger.LogInformation("Customer is an Account");

                var account = (Account)customer;
                var accountType = account.msnfp_accounttype;

                if (accountType == AccountType.Household)
                {
                    this._logger.LogInformation("Customer Account is a Household");

                    // Update household -- sum up the values from the household members
                    UpdateHousehold(account);
                }
                else if (accountType == AccountType.Organization)
                {
                    this._logger.LogInformation("Customer Account is an Organization");

                    // Update organization - update directly from transactions, etc...
                    UpdateIndividualCustomer(customer);
                }
                else
                {
                    // Unknown type
                    this._logger.LogError($"Unknown Account Type for Account Id {customerId}. Exiting.");
                }
            }
            // Customer is Contact
            else
            {
                this._logger.LogInformation("Customer is a Contact");

                // If the Contact is a member of a household, we update the Household (which, in turn, updates all members)
                var contact = (Contact)customer;
                if (contact.msnfp_householdid.HasValue)
                {
                    this._logger.LogInformation("Customer Contact is a member of a Household. Must update entire Household.");

                    if (GetCustomer(contact.msnfp_householdid, 1) is Account household)
                    {
                        UpdateHousehold(household);
                    }
                }
                else
                {
                    this._logger.LogInformation("customer Contact is not a member if a Household. Update just the Contact.");
                    // otherwise Customer is not a member of a household - just update directly from transactions, etc
                    UpdateIndividualCustomer(customer);
                }
            }
        }

        // update individual members can be set to False when UpdateAllMembers is called (i.e. usually happens when the application is called on schedule).
        // when this happens, we'll be iterating through all members anyway, so there's no need to do it again here.
        public void UpdateHousehold(Account household, Boolean updateIndividualMembers = true)
        {
            this._logger.LogInformation("Updating Household id:" + household.AccountId);

            // Retrieve all members of the household (including inactive and deleted members)
            List<Contact> householdMembers = this._dataContext.Contact.Where(c => c.msnfp_householdid == household.AccountId).ToList();

            this._logger.LogInformation($"Household has {householdMembers.Count} members.");

            // First ensure that all the members yearly giving values are updated (if necessary)
            if (updateIndividualMembers)
            {
                this._logger.LogInformation("Updating Household Members");

                foreach (Contact householdMember in householdMembers)
                {
                    // Update the member
                    this._logger.LogInformation($"Household Member Id {householdMember.CustomerId}");
                    UpdateIndividualCustomer(householdMember);
                }
            }

            // Now update the household itself
            this._logger.LogInformation("Updating Household Totals");
            decimal? year0Giving = householdMembers.Sum(c => c.msnfp_year0_giving);
            decimal? year1Giving = householdMembers.Sum(c => c.msnfp_year1_giving);
            decimal? year2Giving = householdMembers.Sum(c => c.msnfp_year2_giving);
            decimal? year3Giving = householdMembers.Sum(c => c.msnfp_year3_giving);
            decimal? year4Giving = householdMembers.Sum(c => c.msnfp_year4_giving);

            // We also update the Lifetime Giving Sum here
            decimal? lifetimeGiving = householdMembers.Sum((c => c.msnfp_lifetimegivingsum));

            bool syncRequired = false;

            // Todo refactor?
            if (!areEqual(household.msnfp_year0_giving, year0Giving))
            {
                household.msnfp_year0_giving = year0Giving;
                syncRequired = true;
            }
            if (!areEqual(household.msnfp_year1_giving, year1Giving))
            {
                household.msnfp_year1_giving = year1Giving;
                syncRequired = true;
            }
            if (!areEqual(household.msnfp_year2_giving, year2Giving))
            {
                household.msnfp_year2_giving = year2Giving;
                syncRequired = true;
            }
            if (!areEqual(household.msnfp_year3_giving, year3Giving))
            {
                household.msnfp_year3_giving = year3Giving;
                syncRequired = true;
            }
            if (!areEqual(household.msnfp_year4_giving, year4Giving))
            {
                household.msnfp_year4_giving = year4Giving;
                syncRequired = true;
            }
            // lifetime giving
            if (!areEqual(household.msnfp_lifetimegivingsum, lifetimeGiving))
            {
                household.msnfp_lifetimegivingsum = lifetimeGiving;
                syncRequired = true;
            }

            if (syncRequired)
            {
                household.SyncDate = null;
                this._dataContext.Update(household);
                this._dataContext.SaveChanges();
            }
        }

        public void UpdateIndividualCustomer(ICustomer customer)
        {
            this._logger.LogInformation($"Entering UpdateIndividualCustomer for Customer with Id:{customer.CustomerId}");

            int currentYear = DateTime.Today.Year;
            List<decimal> yearlyGivingAmounts = new List<decimal>();

            // Get the sum from the qualifying transactions, event packages and donor commitments from each year
            for (int yearCounter = 0; yearCounter < 5; yearCounter++)
            {
                decimal transactionGiving = this._dataContext.Transaction
                    .Where(x => x.CustomerId == customer.CustomerId
                        && x.Deleted != true
                        && x.StatusCode == StatusCode.Completed
                        && x.TypeCode == TransactionTypeCode.Donation
                        && (x.BookDate.HasValue && x.BookDate.Value.Year == currentYear - yearCounter))
                    .Sum(x => x.Amount ?? 0);

                decimal eventPackageGiving = this._dataContext.EventPackage
                    .Where(x => x.CustomerId == customer.CustomerId
                                && x.Deleted != true
                                && x.StatusCode == StatusCode.Completed
                                && (x.Date.HasValue && x.Date.Value.Year == currentYear - yearCounter))
                    .Sum(x => x.Amount ?? 0);

                decimal donorCommitmentGiving = this._dataContext.DonorCommitment
                    .Where(x => x.CustomerId == customer.CustomerId
                                && x.Deleted != true
                                && x.StatusCode == StatusCode.Active
                                && (x.BookDate.HasValue && x.BookDate.Value.Year == currentYear - yearCounter))
                    .Sum(x => x.TotalAmountBalance ?? 0);

                yearlyGivingAmounts.Add(transactionGiving + eventPackageGiving + donorCommitmentGiving);

                this._logger.LogInformation(
                    $"Year:{yearCounter}, " +
                    $"transactionGiving:{transactionGiving}, " +
                    $"eventPackageGiving:{eventPackageGiving}, " +
                    $"donorCommitmentGiving:{donorCommitmentGiving}, " +
                    $"total:{yearlyGivingAmounts[yearCounter]}");
            }

            bool syncRequired = false;

            // Todo refactor?
            if (!areEqual(customer.msnfp_year0_giving, yearlyGivingAmounts[0]))
            {
                this._logger.LogInformation("Updating Year 0 with: " + yearlyGivingAmounts[0]);
                customer.msnfp_year0_giving = yearlyGivingAmounts[0];
                syncRequired = true;
            }
            if (!areEqual(customer.msnfp_year1_giving, yearlyGivingAmounts[1]))
            {
                this._logger.LogInformation("Updating Year 1 with: " + yearlyGivingAmounts[1]);
                customer.msnfp_year1_giving = yearlyGivingAmounts[1];
                syncRequired = true;
            }
            if (!areEqual(customer.msnfp_year2_giving, yearlyGivingAmounts[2]))
            {
                this._logger.LogInformation("Updating Year 2 with: " + yearlyGivingAmounts[2]);
                customer.msnfp_year2_giving = yearlyGivingAmounts[2];
                syncRequired = true;
            }
            if (!areEqual(customer.msnfp_year3_giving, yearlyGivingAmounts[3]))
            {
                this._logger.LogInformation("Updating Year 3 with: " + yearlyGivingAmounts[3]);
                customer.msnfp_year3_giving = yearlyGivingAmounts[3];
                syncRequired = true;
            }
            if (!areEqual(customer.msnfp_year4_giving, yearlyGivingAmounts[4]))
            {
                this._logger.LogInformation("Updating Year 4 with: " + yearlyGivingAmounts[4]);
                customer.msnfp_year4_giving = yearlyGivingAmounts[4];
                syncRequired = true;
            }

            // slightly different calculations for Lifetime Giving
            decimal lifetimeTransactionGiving = this._dataContext.Transaction
                .Where(x => x.CustomerId == customer.CustomerId
                            && x.Deleted != true
                            && x.StatusCode == StatusCode.Completed
                            && x.TypeCode == TransactionTypeCode.Donation)
                .Sum(x => x.Amount ?? 0);

            decimal lifetimeEventPackageGiving = this._dataContext.EventPackage
                .Where(x => x.CustomerId == customer.CustomerId
                            && x.Deleted != true
                            && x.StatusCode == StatusCode.Completed)
                .Sum(x => x.Amount ?? 0);

            decimal lifetimeDonorCommitmentGiving = this._dataContext.DonorCommitment
                .Where(x => x.CustomerId == customer.CustomerId
                            && x.Deleted != true
                            && x.StatusCode == StatusCode.Active)
                .Sum(x => x.TotalAmountBalance ?? 0);

            decimal lifetimeGivingAmount = lifetimeTransactionGiving + lifetimeEventPackageGiving + lifetimeDonorCommitmentGiving;
            this._logger.LogInformation(
                $"lifetimeTransactionGiving:{lifetimeTransactionGiving}, " +
                $"lifetimeEventPackageGiving:{lifetimeEventPackageGiving}, " +
                $"lifetimeGivingAmount:{lifetimeGivingAmount}, " +
                $"lifetimeDonorCommitmentGiving{lifetimeDonorCommitmentGiving}");

            if (!areEqual(customer.msnfp_lifetimegivingsum, lifetimeGivingAmount))
            {
                this._logger.LogInformation("Updating lifetime giving with: " + lifetimeGivingAmount);
                customer.msnfp_lifetimegivingsum = lifetimeGivingAmount;
                syncRequired = true;
            }

            if (syncRequired)
            {
                this._logger.LogInformation("Updating Customer.");
                customer.SyncDate = null;
                this._dataContext.Update(customer);
                this._dataContext.SaveChanges();
            }
        }

        private ICustomer? GetCustomer(Guid? customerId, int? customerIdType)
        {
            ICustomer? customer = null;

            if (!customerId.HasValue)
            {
                this._logger.LogError("Customer Id is Null. Exiting.");
                return null;
            }

            if (customerIdType == CustomerIdType.Account)
            {
                customer = this._dataContext.Account.FirstOrDefault(c => c.AccountId == customerId.Value);
            }
            else if (customerIdType == CustomerIdType.Contact)
            {
                customer = this._dataContext.Contact.FirstOrDefault(c => c.ContactId == customerId.Value);
            }

            return customer;
        }

        public void SynchronizeEntitiesForYearlyGiving()
        {
            this._logger.LogInformation($"Synchronization from dynamics: Start.");
            try
            {
                this._dbEntitySynchronizer.SynchronizeEntitiesToDbTransitively(new List<Type>{
                    typeof(Transaction),
                    typeof(Contact),
                    typeof(Account),
                    typeof(DonorCommitment),
                    typeof(EventPackage)
                });
            }
            catch (Exception e)
            {
                this._logger.LogCritical(e, "Critical error during synchronization from Dataverse");
                throw;
            }

            this._logger.LogInformation($"Synchronization from dynamics: Stop.");
        }

        // in order to cut down on unnecessary syncing, we'll consider null to be equal to zero
        // i.e. if the database value is null, but the value as calculated by the app is zero,
        // we won't bother updating the db.
        bool areEqual(decimal? val1, decimal? val2)
        {
            if (!val1.HasValue) val1 = 0;
            if (!val2.HasValue) val2 = 0;
            if (val1 == val2) return true;
            return false;
        }
    }
}