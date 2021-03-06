﻿using Centaurus.Models;
using Centaurus.DAL.Models;
using Centaurus.DAL.Models.Analytics;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using NLog;
using System.Threading;

namespace Centaurus.DAL.Mongo
{
    public class MongoStorage : IStorage
    {
        static Logger logger = LogManager.GetCurrentClassLogger();

        private MongoClient client;
        private IMongoDatabase database;
        private IMongoCollection<OrderModel> ordersCollection;
        private IMongoCollection<AccountModel> accountsCollection;
        private IMongoCollection<BalanceModel> balancesCollection;
        private IMongoCollection<QuantumModel> quantaCollection;
        private IMongoCollection<ConstellationState> constellationStateCollection;
        private IMongoCollection<EffectsModel> effectsCollection;
        private IMongoCollection<SettingsModel> settingsCollection;
        private IMongoCollection<AssetModel> assetsCollection;

        private IMongoCollection<PriceHistoryFrameModel> priceHistoryCollection;

        private async Task<IMongoCollection<T>> GetCollection<T>(string collectionName)
        {
            if (!(await database.ListCollectionNamesAsync(new ListCollectionNamesOptions { Filter = Builders<BsonDocument>.Filter.Eq("name", collectionName) })).Any())
                await database.CreateCollectionAsync(collectionName);
            return database.GetCollection<T>(collectionName);
        }

        public async Task OpenConnection(string connectionString)
        {
            var mongoUrl = new MongoUrl(connectionString);

            var conventionPack = new ConventionPack { new IgnoreExtraElementsConvention(true) };
            ConventionRegistry.Register("IgnoreExtraElements", conventionPack, type => true);

            client = new MongoClient(mongoUrl);
            database = client.GetDatabase(mongoUrl.DatabaseName);

            ordersCollection = await GetCollection<OrderModel>("orders");
            accountsCollection = await GetCollection<AccountModel>("accounts");
            balancesCollection = await GetCollection<BalanceModel>("balances");
            quantaCollection = await GetCollection<QuantumModel>("quanta");
            constellationStateCollection = await GetCollection<ConstellationState>("constellationState");

            effectsCollection = await GetCollection<EffectsModel>("effects");

            settingsCollection = await GetCollection<SettingsModel>("constellationSettings");

            assetsCollection = await GetCollection<AssetModel>("assets");

            priceHistoryCollection = await GetCollection<PriceHistoryFrameModel>("priceHistory");
        }

        public Task CloseConnection()
        {
            return Task.CompletedTask;
        }

        public async Task<List<OrderModel>> LoadOrders()
        {
            return await ordersCollection
                .Find(FilterDefinition<OrderModel>.Empty)
                .SortBy(o => o.Id)
                .ToListAsync();
        }

        public async Task<SettingsModel> LoadSettings(long apex)
        {
            return await settingsCollection
                .Find(s => s.Apex <= apex)
                .SortByDescending(s => s.Apex)
                .FirstOrDefaultAsync();
        }

        public async Task<List<AssetModel>> LoadAssets(long apex)
        {
            return await assetsCollection
                .Find(a => a.Apex <= apex)
                .SortBy(a => a.Id)
                .ToListAsync();
        }

        public async Task<List<AccountModel>> LoadAccounts()
        {
            return await accountsCollection
                .Find(FilterDefinition<AccountModel>.Empty)
                .SortBy(a => a.Id)
                .ToListAsync();
        }

        public async Task<List<BalanceModel>> LoadBalances()
        {
            return await balancesCollection
                .Find(FilterDefinition<BalanceModel>.Empty)
                .SortBy(b => b.Id)
                .ToListAsync();
        }

        public async Task<ConstellationState> LoadConstellationState()
        {
            return await constellationStateCollection
                .Find(FilterDefinition<ConstellationState>.Empty)
                .FirstOrDefaultAsync();
        }

        public async Task<QuantumModel> LoadQuantum(long apex)
        {
            return await quantaCollection
                   .Find(q => q.Apex == apex)
                   .FirstAsync();
        }

        public async Task<List<QuantumModel>> LoadQuanta(params long[] apexes)
        {
            var filter = FilterDefinition<QuantumModel>.Empty;
            if (apexes.Length > 0)
                filter = Builders<QuantumModel>.Filter.In(q => q.Apex, apexes);

            var res = await quantaCollection
                .Find(filter)
                .SortBy(q => q.Apex)
                .ToListAsync();

            if (res.Count != apexes.Length)
                throw new Exception("Not all quanta were found");

            return res;
        }

        public async Task<List<QuantumModel>> LoadQuantaAboveApex(long apex, int count = 0)
        {
            var query = quantaCollection
                .Find(q => q.Apex > apex);
            if (count > 0)
                query = query.Limit(count);
            return await query
                .SortBy(q => q.Apex)
                .ToListAsync();
        }

        public async Task<long> GetFirstEffectApex()
        {
            var firstEffect = await effectsCollection
                   .Find(FilterDefinition<EffectsModel>.Empty)
                   .SortBy(e => e.Apex)
                   .FirstOrDefaultAsync();
            if (firstEffect == null)
                return -1;

            return firstEffect.Apex;
        }

        public async Task<List<EffectsModel>> LoadEffectsForApex(long apex)
        {
            var fromCursor = EffectModelIdConverter.EncodeId(apex, 0);
            var toCursor = EffectModelIdConverter.EncodeId(apex + 1, 0);
            return await effectsCollection
                .Find(e => e.Apex == apex)
                .SortBy(e => e.Apex)
                .ToListAsync();
        }

        public async Task<List<EffectsModel>> LoadEffectsAboveApex(long apex)
        {
            return await effectsCollection
                .Find(e => e.Apex > apex)
                .SortBy(e => e.Apex)
                .ToListAsync();
        }

        public async Task<long> GetLastApex()
        {
            return (await GetLastQuantum())?.Apex ?? -1;
        }

        private async Task<QuantumModel> GetLastQuantum()
        {
            return await quantaCollection
                   .Find(FilterDefinition<QuantumModel>.Empty)
                   .SortByDescending(e => e.Apex)
                   .FirstOrDefaultAsync();
        }

        public async Task<List<EffectsModel>> LoadEffects(long apex, bool isDesc, int limit, int account)
        {
            if (account == default)
                throw new ArgumentNullException(nameof(account));
            var filter = Builders<EffectsModel>.Filter.Eq(e => e.Account, account);

            if (apex > 0)
            {
                if (isDesc)
                    filter = Builders<EffectsModel>.Filter.And(filter, Builders<EffectsModel>.Filter.Lt(e => e.Apex, apex));
                else
                    filter = Builders<EffectsModel>.Filter.And(filter, Builders<EffectsModel>.Filter.Gt(e => e.Apex, apex));
            }

            var query = effectsCollection
                    .Find(filter);

            if (isDesc)
                query = query
                    .SortByDescending(e => e.Apex);
            else
                query = query
                    .SortBy(e => e.Apex);

            var effects = await query
                .Limit(limit)
                .ToListAsync();

            return effects;
        }


        public async Task<List<PriceHistoryFrameModel>> GetPriceHistory(int cursorTimeStamp, int toUnixTimeStamp, int asset, PriceHistoryPeriod period)
        {
            var cursorId = PriceHistoryExtensions.EncodeId(asset, (int)period, cursorTimeStamp);
            var toId = PriceHistoryExtensions.EncodeId(asset, (int)period, toUnixTimeStamp);
            var query = priceHistoryCollection.Find(
                   Builders<PriceHistoryFrameModel>.Filter.And(
                       Builders<PriceHistoryFrameModel>.Filter.Gte(f => f.Id, cursorId),
                       Builders<PriceHistoryFrameModel>.Filter.Lt(f => f.Id, toId)
                       )
                   ).SortByDescending(x => x.Id);
            return await query
                .ToListAsync();
        }

        public async Task<int> GetFirstPriceHistoryFrameDate(int market, PriceHistoryPeriod period)
        {
            var firstId = PriceHistoryExtensions.EncodeId(market, (int)period, 0);

            var firstFrame = await priceHistoryCollection
                .Find(Builders<PriceHistoryFrameModel>.Filter.Gte(f => f.Id, firstId))
                .SortBy(e => e.Id)
                .FirstOrDefaultAsync();

            if (firstFrame == null)
                return 0;

            return PriceHistoryExtensions.DecodeId(firstFrame.Id).timestamp;
        }

        #region Updates

        public async Task<int> Update(DiffObject update)
        {
            var stellarUpdates = GetStellarDataUpdate(update.StellarInfoData);
            var accountUpdates = GetAccountUpdates(update.Accounts.Values.ToList());
            var balanceUpdates = GetBalanceUpdates(update.Balances.Values.ToList());
            var orderUpdates = GetOrderUpdates(update.Orders.Values.ToList());
            var quanta = update.Quanta.Select(a => a.Quantum);
            var effects = update.Quanta.SelectMany(a => a.Effects.Values);

            var retries = 1;
            var maxTries = 5;
            var isCommitInvoked = false;
            while (true)
            {
                using (var session = await client.StartSessionAsync())
                {
                    try
                    {
                        session.StartTransaction();
                        try
                        {
                            var updateTasks = new List<Task>();

                            if (stellarUpdates != null)
                                updateTasks.Add(constellationStateCollection.BulkWriteAsync(session, new WriteModel<ConstellationState>[] { stellarUpdates }));

                            if (update.ConstellationSettings != null)
                                updateTasks.Add(settingsCollection.InsertOneAsync(session, update.ConstellationSettings));

                            if (update.Assets != null && update.Assets.Count > 0)
                                updateTasks.Add(assetsCollection.InsertManyAsync(session, update.Assets));

                            if (accountUpdates != null)
                                updateTasks.Add(accountsCollection.BulkWriteAsync(session, accountUpdates));

                            if (balanceUpdates != null)
                                updateTasks.Add(balancesCollection.BulkWriteAsync(session, balanceUpdates));

                            if (orderUpdates != null)
                                updateTasks.Add(ordersCollection.BulkWriteAsync(session, orderUpdates));

                            SaveQuanta(ref updateTasks, quanta, session);

                            SaveEffects(ref updateTasks, effects, session);

                            await Task.WhenAll(updateTasks);

                            isCommitInvoked = true;
                            await CommitWithRetry(session);

                            break;
                        }
                        catch
                        {
                            if (!isCommitInvoked)
                            {
                                try
                                {
                                    await session.AbortTransactionAsync();
                                }
                                catch { } //MongoDB best practice ;)
                            }
                            throw;
                        }
                    }
                    catch (Exception exc)
                    {
                        if (retries <= maxTries)
                        {
                            logger.Debug(exc, $"Error during update. {retries} try.");
                            retries++;
                            continue;
                        }
                        new Exception($"Unable to commit transaction after {retries} retries", exc);
                    }
                }
            }
            return retries;
        }

        private void SaveQuanta(ref List<Task> updateTasks, IEnumerable<QuantumModel> quanta, IClientSessionHandle session)
        {
            var batchSize = 20_000;

            var savedQuantaCount = 0;
            var quantaCount = quanta.Count();
            while (savedQuantaCount < quantaCount)
            {
                updateTasks.Add(quantaCollection.InsertManyAsync(
                    session,
                    quanta.Skip(savedQuantaCount).Take(batchSize),
                    new InsertManyOptions { BypassDocumentValidation = true, IsOrdered = false })
                );
                savedQuantaCount += batchSize;
            }
        }

        private void SaveEffects(ref List<Task> updateTasks, IEnumerable<EffectsModel> effects, IClientSessionHandle session)
        {
            var maxBatchEffectsCount = 20_000;
            var savedEffectsCount = 0;
            var effectsCount = effects.Count();
            while (savedEffectsCount < effectsCount)
            {
                var currentBatchEffectsCount = 0;
                var currentBatchEffects = effects.Skip(savedEffectsCount).TakeWhile(e =>
                {
                    if (currentBatchEffectsCount > maxBatchEffectsCount)
                        return false;
                    currentBatchEffectsCount += e.Effects.Count;
                    return true;
                }).ToList();

                updateTasks.Add(
                    effectsCollection.InsertManyAsync(
                        session,
                        currentBatchEffects,
                        new InsertManyOptions { BypassDocumentValidation = true, IsOrdered = false })
                    );
                savedEffectsCount += currentBatchEffects.Count;
            }
        }

        static string[] retriableCommitErrors = new string[] { "UnknownTransactionCommitResult", "TransientTransactionError " };

        private static async Task CommitWithRetry(IClientSessionHandle session)
        {
            var maxTries = 5;
            var retries = 0;
            while (true)
            {
                try
                {
                    await session.CommitTransactionAsync();
                    break;
                }
                catch (MongoException exc)
                {
                    if (!(exc is MongoException mongoException
                            && mongoException.ErrorLabels.Any(l => retriableCommitErrors.Contains(l))))
                        throw;
                    retries++;
                    if (retries < maxTries)
                    {
                        logger.Warn(exc, $"Error on commit. Labels: {string.Join(',', mongoException.ErrorLabels)}. {retries} try.");
                        continue;
                    }
                    throw new UnknownCommitResultException($"UnknownTransactionCommitResult or TransientTransactionError errors occurred after {retries} retries", exc);
                }
            }
        }

        private WriteModel<ConstellationState> GetStellarDataUpdate(DiffObject.ConstellationState constellationState)
        {
            if (constellationState == null)
                return null;
            var cursor = constellationState.TxCursor;

            WriteModel<ConstellationState> updateModel = null;
            if (constellationState.IsInserted)
                updateModel = new InsertOneModel<ConstellationState>(new ConstellationState
                {
                    TxCursor = cursor
                });
            else if (constellationState.IsDeleted)
                throw new InvalidOperationException("Stellar data entry cannot be deleted");
            else
            {
                updateModel = new UpdateOneModel<ConstellationState>(Builders<ConstellationState>.Filter.Empty, Builders<ConstellationState>.Update.Set(s => s.TxCursor, cursor));
            }

            return updateModel;
        }

        private WriteModel<AccountModel>[] GetAccountUpdates(List<DiffObject.Account> accounts)
        {
            if (accounts == null || accounts.Count < 1)
                return null;
            var filter = Builders<AccountModel>.Filter;
            var update = Builders<AccountModel>.Update;

            var accLength = accounts.Count;
            var updates = new WriteModel<AccountModel>[accLength];

            for (int i = 0; i < accLength; i++)
            {
                var acc = accounts[i];
                var currentAccFilter = filter.Eq(a => a.Id, acc.Id);
                if (acc.IsInserted)
                    updates[i] = new InsertOneModel<AccountModel>(new AccountModel
                    {
                        Id = acc.Id,
                        Nonce = acc.Nonce,
                        PubKey = acc.PubKey,
                        RequestRateLimits = acc.RequestRateLimits,
                        Withdrawal = (acc.Withdrawal.HasValue ? acc.Withdrawal.Value : 0)
                    });
                else if (acc.IsDeleted)
                    updates[i] = new DeleteOneModel<AccountModel>(currentAccFilter);
                else
                {
                    var updateDefs = new List<UpdateDefinition<AccountModel>>();
                    if (acc.Nonce != 0)
                        updateDefs.Add(update.Set(a => a.Nonce, acc.Nonce));

                    if (acc.RequestRateLimits != null)
                        updateDefs.Add(update.Set(a => a.RequestRateLimits, acc.RequestRateLimits));

                    if (acc.Withdrawal.HasValue)
                        updateDefs.Add(update.Set(a => a.Withdrawal, acc.Withdrawal.Value));

                    updates[i] = new UpdateOneModel<AccountModel>(currentAccFilter, update.Combine(updateDefs));
                }
            }
            return updates;
        }

        private WriteModel<BalanceModel>[] GetBalanceUpdates(List<DiffObject.Balance> balances)
        {
            if (balances == null || balances.Count < 1)
                return null;
            var filter = Builders<BalanceModel>.Filter;
            var update = Builders<BalanceModel>.Update;

            var balancesLength = balances.Count;
            var updates = new WriteModel<BalanceModel>[balancesLength];

            for (int i = 0; i < balancesLength; i++)
            {
                var balance = balances[i];
                var currentBalanceFilter = filter.Eq(s => s.Id, balance.Id);

                if (balance.IsInserted)
                    updates[i] = new InsertOneModel<BalanceModel>(new BalanceModel
                    {
                        Id = balance.Id,
                        Amount = balance.AmountDiff,
                        Liabilities = balance.LiabilitiesDiff
                    });
                else if (balance.IsDeleted)
                    updates[i] = new DeleteOneModel<BalanceModel>(currentBalanceFilter);
                else
                {
                    updates[i] = new UpdateOneModel<BalanceModel>(
                        currentBalanceFilter,
                        update
                            .Inc(b => b.Amount, balance.AmountDiff)
                            .Inc(b => b.Liabilities, balance.LiabilitiesDiff)
                        );
                }
            }
            return updates;
        }

        private WriteModel<OrderModel>[] GetOrderUpdates(List<DiffObject.Order> orders)
        {
            if (orders == null || orders.Count < 1)
                return null;
            unchecked
            {
                var filter = Builders<OrderModel>.Filter;
                var update = Builders<OrderModel>.Update;

                var ordersLength = orders.Count;
                var updates = new WriteModel<OrderModel>[ordersLength];

                for (int i = 0; i < ordersLength; i++)
                {
                    var order = orders[i];

                    var currentOrderFilter = filter.And(filter.Eq(s => s.Id, (long)order.OrderId));

                    if (order.IsInserted)
                        updates[i] = new InsertOneModel<OrderModel>(new OrderModel
                        {
                            Id = (long)order.OrderId,
                            Amount = order.AmountDiff,
                            QuoteAmount = order.QuoteAmountDiff,
                            Price = order.Price,
                            Account = order.Account
                        });
                    else if (order.IsDeleted)
                        updates[i] = new DeleteOneModel<OrderModel>(currentOrderFilter);
                    else
                    {
                        updates[i] = new UpdateOneModel<OrderModel>(
                            currentOrderFilter,
                            update.Inc(b => b.Amount, order.AmountDiff).Inc(b => b.QuoteAmount, order.QuoteAmountDiff)
                        );
                    }
                }
                return updates;
            }
        }

        public async Task SaveAnalytics(List<PriceHistoryFrameModel> frames)
        {
            await priceHistoryCollection.BulkWriteAsync(PrepareFramesUpdateBatch(frames));
        }

        private List<ReplaceOneModel<PriceHistoryFrameModel>> PrepareFramesUpdateBatch(List<PriceHistoryFrameModel> frames)
        {
            var updates = new List<ReplaceOneModel<PriceHistoryFrameModel>>();
            var filter = Builders<PriceHistoryFrameModel>.Filter;
            foreach (var frame in frames)
                updates.Add(new ReplaceOneModel<PriceHistoryFrameModel>(filter.Eq(f => f.Id, frame.Id), frame) { IsUpsert = true });
            return updates;
        }

        public async Task DropDatabase()
        {
            await client.DropDatabaseAsync(database.DatabaseNamespace.DatabaseName);
        }

        #endregion
    }
}
