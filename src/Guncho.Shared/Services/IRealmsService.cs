using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Guncho.Shared.Models;

namespace Guncho.Shared.Services
{
    public interface IRealmsService
    {
        Task<IEnumerable<RealmSummaryDto>> GetAllRealmsAsync();
        Task<RealmDto?> GetRealmByNameAsync(string name);
        Task<RealmDto?> CreateRealmAsync(int ownerId, CreateRealmDto createDto);
        Task<bool> UpdateRealmAsync(string name, RealmDto realm);
        Task<bool> DeleteRealmAsync(string name);
        Task<IEnumerable<RealmFactoryDto>> GetRealmFactoriesAsync();
        Task<bool> IsValidNameChangeAsync(string oldName, string newName);
    }
}
