﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Threading.Tasks;

namespace Ef.Factory
{
    public class UnitWork<T> : IUnitOfWork<T> where T : DbContext, new()
    {
        #region Fields

        private bool _isDisposed;

        #endregion

        #region Properties

        protected T Context { get; set; }

        protected IDictionary<Type, IConmited> CreatedFactories { get; set; }

        public bool AutoCommit { get; set; }

        #endregion

        #region Constructors

        public UnitWork()
        {
            Context = new T();
            CreatedFactories = new Dictionary<Type, IConmited>();
        }

        ~UnitWork()
        {
            Dispose(false);
        }

        #endregion

        #region Factory Operations

        private GenericFactory<TEntity, TKey> CreateFactoryCore<TEntity, TKey>() where TEntity : class
        {
            var disposable = CreatedFactories.ContainsKey(typeof(TEntity))
                ? CreatedFactories[typeof(TEntity)]
                : null;

            if (disposable != null)
            {
                if (!disposable.IsDisposed)
                {
                    return (GenericFactory<TEntity, TKey>) disposable;
                }

                CreatedFactories.Remove(typeof(TEntity));
            }

            var constructorInfo =
                GetRepositoryType().MakeGenericType(typeof (TEntity), typeof (TKey)).GetConstructor(new[]
                {
                    typeof (T), typeof(bool)
                });

            if (constructorInfo != null)
            {
                var repository = (GenericFactory<TEntity, TKey>) constructorInfo.Invoke(new object[]
                {
                    Context, AutoCommit
                });

                CreatedFactories.Add(typeof(TEntity), repository);

                return repository;
            }

            return null;
        }

        protected virtual Type GetRepositoryType()
        {
            return typeof(GenericFactory<,>);
        }

        public IGenericFactoryAsync<TEntity, TKey> CreateFactory<TEntity, TKey>() where TEntity : class
        {
            return CreateFactoryCore<TEntity, TKey>();
        }

        #endregion

        #region UnitOfWork Operations

        public T GetContext
        {
            get { return Context; }
        }

        public void SetContext(T cont)
        {
            Context = cont;
        }
        
        public int Commit(bool autoRollbackOnError = true)
        {
            try
            {
                return Context.SaveChanges();
            }
            catch (Exception)
            {
                if (autoRollbackOnError)
                {
                    RollBack();
                }

                throw;
            }
        }

        public async Task<int> CommitAsync(bool autoRollbackOnError = true)
        {
            try
            {
                return await Context.SaveChangesAsync();
            }
            catch (Exception)
            {
                if (autoRollbackOnError)
                {
                    RollBack();
                }

                throw;
            }
        }

        public void RollBack()
        {
            var ctx = ((IObjectContextAdapter) Context).ObjectContext;
            ctx.AcceptAllChanges();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                if (!CollectionUtils.IsNullOrEmpty(CreatedFactories))
                {
                    foreach (var disposable in CreatedFactories.Values)
                    {
                        disposable.Dispose();
                    }

                    CreatedFactories.Clear();
                }

                Context.Dispose();
                _isDisposed = true;
            }
        }

        #endregion
    }
}