using Ardalis.Specification.EntityFrameworkCore;

namespace FileShare.Infrastructure.Data;

public sealed class EfRepository<T> : RepositoryBase<T> where T : class
{
    public EfRepository(ApplicationDbContext db) : base(db) { }
}
