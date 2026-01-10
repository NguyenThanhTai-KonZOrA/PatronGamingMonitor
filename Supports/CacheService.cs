using PatronGamingMonitor.Models;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PatronGamingMonitor.Supports
{
    public class CacheService
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static CacheService _instance;
        private static readonly object _lock = new object();
        private List<LevyTicket> _cachedTickets = new List<LevyTicket>();
        private DateTime _lastCacheUpdate = DateTime.MinValue;

        private CacheService() { }

        public static CacheService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new CacheService();
                        }
                    }
                }
                return _instance;
            }
        }

        public void SetCache(List<LevyTicket> tickets)
        {
            lock (_lock)
            {
                _cachedTickets = tickets ?? new List<LevyTicket>();
                _lastCacheUpdate = DateTime.Now;
                Logger.Info("Cache updated with {Count} tickets at {Time}",
                    _cachedTickets.Count, _lastCacheUpdate);
            }
        }

        public List<LevyTicket> GetAllTickets()
        {
            lock (_lock)
            {
                return _cachedTickets.ToList();
            }
        }

        public void UpdateTicket(LevyTicket updatedTicket)
        {
            lock (_lock)
            {
                var existing = _cachedTickets.FirstOrDefault(t => t.TransactionNo == updatedTicket.TransactionNo);
                if (existing != null)
                {
                    existing.RemainingTime = updatedTicket.RemainingTime;
                    existing.UsedStatus = updatedTicket.UsedStatus;
                    existing.IsUpdated = true;
                    Logger.Info("Ticket updated in cache: {TransactionNo}", updatedTicket.TransactionNo);
                }
            }
        }

        public void AddTicket(LevyTicket newTicket)
        {
            lock (_lock)
            {
                newTicket.IsNew = true;
                _cachedTickets.Insert(0, newTicket);
                Logger.Info("New ticket added to cache: {TransactionNo}", newTicket.TransactionNo);
            }
        }

        public void RemoveTicket(string transactionNo)
        {
            lock (_lock)
            {
                var ticket = _cachedTickets.FirstOrDefault(t => t.TransactionNo == transactionNo);
                if (ticket != null)
                {
                    _cachedTickets.Remove(ticket);
                    Logger.Info("Ticket removed from cache: {TransactionNo}", transactionNo);
                }
            }
        }

        public void ClearCache()
        {
            lock (_lock)
            {
                _cachedTickets.Clear();
                _lastCacheUpdate = DateTime.MinValue;
                Logger.Info("🗑️ Cache cleared");
            }
        }

        public bool HasCache()
        {
            lock (_lock)
            {
                return _cachedTickets.Any();
            }
        }

        public DateTime GetLastCacheUpdateTime()
        {
            lock (_lock)
            {
                return _lastCacheUpdate;
            }
        }
    }
}