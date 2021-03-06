﻿using Centaurus.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Centaurus.Exchange.Analytics
{
    public class MarketTickersManager
    {
        public MarketTickersManager(List<int> markets, PriceHistoryManager framesManager)
        {
            this.framesManager = framesManager ?? throw new ArgumentNullException(nameof(framesManager));
            period = Enum.GetValues(typeof(PriceHistoryPeriod)).Cast<PriceHistoryPeriod>().Min();
            this.markets = markets;
        }

        public async Task Update()
        {
            try
            {
                var updateDate = DateTime.UtcNow;
                await syncRoot.WaitAsync();
                foreach (var market in markets)
                {
                    if (!tickers.TryGetValue(market, out var currentTicker))
                    {
                        currentTicker = new MarketTicker(market);
                        tickers.Add(market, currentTicker);
                    }
                    await UpdateTicker(currentTicker, updateDate);
                }
            }
            finally
            {
                syncRoot.Release();
            }
        }

        private SemaphoreSlim syncRoot = new SemaphoreSlim(1);
        private PriceHistoryPeriod period;
        private List<int> markets;
        private PriceHistoryManager framesManager;
        private Dictionary<int, MarketTicker> tickers = new Dictionary<int, MarketTicker>();

        private async Task UpdateTicker(MarketTicker marketTicker, DateTime updateDate)
        {
            var frames = await framesManager.GetPriceHistory(0, marketTicker.Market, period);
            var fromDate = DateTime.UtcNow.AddDays(-1);
            var framesFor24Hours = frames.frames.TakeWhile(f => f.StartTime >= fromDate);
            if (framesFor24Hours.Count() < 1)
                return;

            marketTicker.Open = framesFor24Hours.Last().Open;
            marketTicker.Close = framesFor24Hours.First().Close;
            marketTicker.High = framesFor24Hours.Select(f => f.High).Max();
            marketTicker.Low = framesFor24Hours.Select(f => f.Low).Min();
            marketTicker.BaseVolume = framesFor24Hours.Sum(f => f.BaseVolume);
            marketTicker.CounterVolume = framesFor24Hours.Sum(f => f.CounterVolume);
            marketTicker.UpdatedAt = updateDate;
        }

        public MarketTicker GetMarketTicker(int market)
        {
            try
            {
                syncRoot.Wait();
                if (!tickers.TryGetValue(market, out var ticker))
                    throw new Exception($"Market {market} is not supported.");
                return ticker;
            }
            finally
            {
                syncRoot.Release();
            }
        }

        public List<MarketTicker> GetAllTickers()
        {

            try
            {
                syncRoot.Wait();
                return tickers.Values.ToList();
            }
            finally
            {
                syncRoot.Release();
            }
        }
    }
}