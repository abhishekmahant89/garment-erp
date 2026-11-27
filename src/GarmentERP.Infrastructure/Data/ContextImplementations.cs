using Microsoft.EntityFrameworkCore;

namespace GarmentERP.Infrastructure.Data;

public class GarmentServerDbContext : GarmentDbContextBase
{
    public GarmentServerDbContext(DbContextOptions<GarmentServerDbContext> options)
        : base(options)
    {
    }
}

public class GarmentOfflineDbContext : GarmentDbContextBase
{
    public GarmentOfflineDbContext(DbContextOptions<GarmentOfflineDbContext> options)
        : base(options)
    {
    }
}
