using UnityEngine;
using System.Collections.Generic;
using Game.Domain.Entities;

namespace Game.Application.Ports.Out
{
    public interface ILocationRepository
    {
        LocationScreenData GetLocationScreenData(string regionId);
    }
}

