using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MongoDB.Driver;
using MongoDB.Bson;
using PriceTracker.Domain.IRepositories.Common;
using PriceTracker.Domain.Models.Entities.Common;
using Bill.Infrastracture.Models.Options;

namespace PriceTracker.Infrastracture.Repositories.Common
{
    public class ReadRepository<T> : IReadRepository<T> where T : BaseReadEntity
    {
        private readonly MongoDbOptions _options;
        public IMongoCollection<T> _collection { get; }

        public ReadRepository(IOptions<MongoDbOptions> options)
        {
            _options = options.Value;
            var client = new MongoClient(_options.ConnectionString);
            var db = client.GetDatabase(_options.DatabaseName);
            _collection = db.GetCollection<T>(typeof(T).Name.ToLowerInvariant());
        }


        public virtual IQueryable<T> Get(Expression<Func<T, bool>> predicate = null!)
        {
            return predicate == null
                ? _collection.AsQueryable()
                : _collection.AsQueryable().Where(predicate);
        }

        public async Task<List<T>> GetAsyncCustom(
            Expression<Func<T, bool>> predicate = null!,
            FindOptions<T>? findOptions = null)
        {
            var cursor = predicate == null
                ? await _collection.FindAsync(Builders<T>.Filter.Empty, findOptions)
                : await _collection.FindAsync(predicate, findOptions);

            return await cursor.ToListAsync();
        }

        public virtual Task<T> GetAsync(Expression<Func<T, bool>> predicate)
        {
            return _collection.Find(predicate).FirstOrDefaultAsync();
        }

        public virtual Task<List<T>> GetAllAsync(Expression<Func<T, bool>> predicate)
        {
            if (predicate is null)
                return _collection.Find(new BsonDocument()).ToListAsync();
            else
                return _collection.Find(predicate).ToListAsync();
        }

        public virtual Task<T> GetByIdAsync(string id)
        {
            return _collection.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public virtual async Task<T> AddAsync(T entity)
        {
            var options = new InsertOneOptions { BypassDocumentValidation = false };
            await _collection.InsertOneAsync(entity, options);
            return entity;
        }

        public async Task AddManyAsync(IEnumerable<T> entities)
        {
            var options = new InsertManyOptions { BypassDocumentValidation = false };

            await _collection.InsertManyAsync(entities, options);
        }

        public virtual async Task<T> UpdateAsync(string id, T entity)
        {
            return await _collection.FindOneAndReplaceAsync(x => x.Id == id, entity);
        }

        public virtual async Task<T> UpdateAsync(T entity, Expression<Func<T, bool>> predicate)
        {
            return await _collection.FindOneAndReplaceAsync(predicate, entity);
        }

        public virtual async Task<T> RemoveAsync(T entity)
        {
            return await _collection.FindOneAndDeleteAsync(x => x.Id == entity.Id);
        }

        public virtual async Task RemoveManyAsync(List<string> ids)
        {
            await _collection.DeleteManyAsync(x => ids.Contains(x.Id));
        }

        public virtual async Task<T> RemoveAsync(string id)
        {
            return await _collection.FindOneAndDeleteAsync(x => x.Id == id);
        }

        public virtual async Task<T> RemoveAsync(Expression<Func<T, bool>> filter)
        {
            return await _collection.FindOneAndDeleteAsync(filter);
        }
    }
}
