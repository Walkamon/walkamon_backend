namespace DAL.Interfaces
{
    public interface IGenericRepository<T> where T : class
    {
        Task<IEnumerable<T>> GetAllAsync();

        Task<T> GetByIdAsync(long id);

        Task AddAsync(T entity);

        void Update(T entity);

        void Delete(T entity);

        Task SaveAsync();
    }
}
