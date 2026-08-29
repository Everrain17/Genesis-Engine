using System.Linq;
using GenesisEngine.Core;
using GenesisEngine.Entities;
using GenesisEngine.Systems.Observers;
using GenesisEngine.Systems.Physics;

namespace GenesisEngine.Systems
{
    public static class TradeSystem
    {
        /// <summary>
        /// Вычисляет "ценность" предмета для агента.
        /// Зависит от свойств материала и потребностей агента.
        /// </summary>
        private static float CalculateValue(WorldObject obj, Agent agent)
        {
            if (!MaterialDB.TryGet(obj.MaterialId, out var spec))
                return 1f;

            // Базовая ценность = редкость + качество
            float baseValue = 1f + spec.Rarity * 2f + spec.Hardness + spec.Logic;

            // Потребности агента увеличивают ценность
            float needMultiplier = 1f;

            // Голод → еда ценнее
            if (agent.Body.Hunger > 50f && spec.Organic > 0.5f)
                needMultiplier += 1.5f;

            // Низкое здоровье → лечебные материалы ценнее
            if (agent.Body.Health < 50f && spec.Organic > 0.6f)
                needMultiplier += 1.2f;

            // Строитель → твёрдые материалы ценнее
            if (agent.Role == AgentRole.Builder && spec.Hardness > 0.6f)
                needMultiplier += 0.8f;

            // Учёный → логические материалы ценнее
            if (agent.Logic > 0.5f && spec.Logic > 0.5f)
                needMultiplier += 0.6f;

            return baseValue * needMultiplier;
        }

        /// <summary>
        /// Эмерджентная торговля с учётом ценности предметов.
        /// Агенты торгуют, если оба выигрывают.
        /// </summary>
        public static bool AutoTrade(Agent agent)
        {
            if (agent == null)
                return false;

            var nearby = SpatialGrid.GetNearby(agent.Position, 2);
            Agent partner = null;

            // Ищем партнёра с доверием и ресурсами
            foreach (var p in nearby)
            {
                if (p == null || p.Id == agent.Id)
                    continue;

                if (agent.Memory.GetTrust(p.Id) <= -20f)
                    continue;

                if (p.Body.Inventory.Any(o => o.Quantity >= 1f))
                {
                    partner = p;
                    break;
                }
            }

            if (partner == null)
                return false;

            // Находим лучший предмет для торговли у себя
            var myBestItem = agent.Body.Inventory
                .Where(o => o.Quantity >= 1f)
                .OrderByDescending(o => CalculateValue(o, partner)) // То, что ЦЕННО для партнёра
                .FirstOrDefault();

            if (myBestItem == null)
                return false;

            // Находим лучший предмет у партнёра
            var theirBestItem = partner.Body.Inventory
                .Where(o => o.Quantity >= 1f && o.MaterialId != myBestItem.MaterialId)
                .OrderByDescending(o => CalculateValue(o, agent)) // То, что ЦЕННО для меня
                .FirstOrDefault();

            if (theirBestItem == null)
                return false;

            // Вычисляем ценность обмена для каждого
            float myValueGiven = CalculateValue(myBestItem, agent);    // Что я отдаю (для МЕНЯ)
            float myValueReceived = CalculateValue(theirBestItem, agent); // Что я получаю (для МЕНЯ)

            float theirValueGiven = CalculateValue(theirBestItem, partner); // Что они отдают (для НИХ)
            float theirValueReceived = CalculateValue(myBestItem, partner);  // Что они получают (для НИХ)

            // Выгода для меня
            float myGain = myValueReceived - myValueGiven;
            // Выгода для партнёра
            float theirGain = theirValueReceived - theirValueGiven;

            // Торговля происходит, если ОБА выигрывают (или хотя бы не проигрывают сильно)
            // Небольшой дисбаланс допустим (0.5) — агенты не идеальны
            if (myGain >= -0.5f && theirGain >= -0.5f)
            {
                if (!agent.Body.CanCarry(1f) || !partner.Body.CanCarry(1f))
                    return false;

                // Обмен
                myBestItem.Quantity -= 1f;
                theirBestItem.Quantity -= 1f;

                agent.Body.Inventory.Add(new WorldObject
                {
                    MaterialId = theirBestItem.MaterialId,
                    Quantity = 1f,
                    HolderId = agent.Id
                });

                partner.Body.Inventory.Add(new WorldObject
                {
                    MaterialId = myBestItem.MaterialId,
                    Quantity = 1f,
                    HolderId = partner.Id
                });

                if (myBestItem.Quantity <= 0f)
                    agent.Body.Inventory.Remove(myBestItem);

                if (theirBestItem.Quantity <= 0f)
                    partner.Body.Inventory.Remove(theirBestItem);

                // Увеличиваем доверие
                float trustGain = 15f + (myGain + theirGain) * 2f; // Чем выгоднее сделка, тем больше доверия
                agent.Memory.UpdateAgentMemory(partner.Id, "Trade", Math.Max(5f, trustGain));
                partner.Memory.UpdateAgentMemory(agent.Id, "Trade", Math.Max(5f, trustGain));

                EventBus.Publish(new SimEvent
                {
                    Type = SimEventType.Trade,
                    Tick = Simulation.Instance.TotalTicks,
                    Actor = agent,
                    Target = partner,
                    Position = agent.Position,
                    Value = myGain + theirGain // Общая выгода от сделки
                });

                agent.LastAction = "Trade";
                return true;
            }

            return false;
        }
    }
}