﻿using System;
using System.Net;
using System.Net.Http;
using FundraisingandEngagement.DataFactory;
using FundraisingandEngagement.DataFactory.Workers;
using FundraisingandEngagement.Models.Entities;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MembershipGroupController : ControllerBase
    {
        private static IFactoryFloor<MembershipGroup> _membershipGroupWorker;

        public MembershipGroupController(IDataFactory dataFactory)
        {
            _membershipGroupWorker = dataFactory.GetDataFactory<MembershipGroup>();
        }

        // POST api/MembershipGroup/CreateMembershipGroup (Body is JSON)
        [HttpPost]
        [Route("CreateMembershipGroup")]
        public HttpResponseMessage CreateMembershipGroup(MembershipGroup createRecord)
        {
            try
            {
                if (createRecord == null)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                // Create the entity record in the Azure SQL DB:
                int createResult = _membershipGroupWorker.UpdateCreate(createRecord);
                if (createResult > 0)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
                // Existed already:
                else if (createResult == 0)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                return new HttpResponseMessage(HttpStatusCode.OK);

            }
            catch (Exception e)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
        }


        // POST api/MembershipGroup/UpdateMembershipGroup (Body is JSON)
        [HttpPost]
        [Route("UpdateMembershipGroup")]
        public HttpResponseMessage UpdateMembershipGroup(MembershipGroup updatedRecord)
        {
            try
            {
                if (updatedRecord == null)
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                // Create the entity record in the Azure SQL DB:
                int updateResult = _membershipGroupWorker.UpdateCreate(updatedRecord);
                if (updateResult > 0)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }
                // Existed already:
                else if (updateResult == 0)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                return new HttpResponseMessage(HttpStatusCode.OK);

            }
            catch (Exception e)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }
        }


        // DELETE api/MembershipGroup/5
        [HttpDelete("{id}")]
        public void Delete(Guid id)
        {
            if (id != null)
            {
                _membershipGroupWorker.Delete(id);
            }
        }
    }
}