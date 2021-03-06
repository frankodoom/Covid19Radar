﻿using Covid19Radar.DataStore;
using Covid19Radar.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Covid19Radar.DataAccess
{
    public class CosmosSequenceRepository : ISequenceRepository
    {
        private readonly ICosmos _db;
        private readonly ILogger<CosmosSequenceRepository> _logger;

        public CosmosSequenceRepository(ICosmos db, ILogger<CosmosSequenceRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<int> GetNextAsync(string key, int startNo)
        {
            var pk = new PartitionKey(key);
            for (var i = 0; i < 100; i++)
            {
                try
                {
                    var r = await _db.Sequence.ReadItemAsync<SequenceModel>(key, pk);
                    r.Resource.value++;
                    var opt = new RequestOptions();
                    opt.IfMatchEtag = r.ETag;
                    var rReplece = await _db.Sequence.ReplaceItemAsync(r.Resource, key, pk);
                    return r.Resource.value;
                }
                catch (CosmosException ex)
                {
                    if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        var resultCreate = await _db.Sequence.CreateItemAsync(new SequenceModel()
                        {
                            id = key,
                            PartitionKey = key,
                            value = startNo
                        }, pk);
                        return startNo;
                    }
                    _logger.LogInformation(ex, $"GetNextAsync Retry {i}");
                    await Task.Delay(100);
                    continue;
                }
            }
            _logger.LogWarning("GetNextAsync is over retry count.");
            throw new ApplicationException("GetNextAsync is over retry count.");
        }
    }
}
