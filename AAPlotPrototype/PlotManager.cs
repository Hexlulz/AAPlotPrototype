using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using AAPlotPrototype.Models.Plots.Static;
using System.Diagnostics;
using AAPlotPrototype.Models.Translations;

namespace AAPlotPrototype.Models.Plots
{
    public class PlotManager
    {
        Dictionary<uint, Plot> _plots;
        Dictionary<uint, PlotCondition> _conditions;
        Dictionary<uint, PlotEffect> _effects;
        Dictionary<uint, PlotNextEvent> _nextEvents;
        Dictionary<uint, PlotEvent> _events;

        //translations
        Dictionary<uint, Tag> _tags;
        Dictionary<uint, BuffEffect> _buffEffects;

        Dictionary<uint, uint> _tickets;

        uint _previousEvent;
        int _previousDepth;
        bool depthFixNeeded;

        private void PrintWithScope(string data, int depth)
        {
            for (int i = 0; i < depth; i++)
                Console.Write("   ");
            Console.Write($"{data}\n");
        }
        private void AnalyzeEffects(uint eventId, int depth)
        {
            foreach (var effect in _effects.Values)
            {
                if (effect.EventId == eventId)
                {
                    switch (effect.ActualType)
                    {
                        case "BuffEffect":
                            PrintWithScope($"Effect[{effect.ActualId}] Add Buff [{_buffEffects[effect.ActualId].Name}]", depth);
                        break;
                        default:
                            PrintWithScope($"Effect[{effect.ActualId}] Kind={effect.ActualType} ", depth);
                        break;
                    }
                }
            }
        }
        private bool IsConditioned(uint eventId)
        {
            foreach (var condition in _conditions.Values)
                if (condition.EventId == eventId)
                    return true;

            return false;
        }
        private void AnalyzeConditions(uint eventId, int depth)
        {
            bool conditioned = false;
            foreach (var condition in _conditions.Values)
            {
                if (condition.EventId == eventId)
                {
                    switch (condition.Kind_id)
                    {
                        case PlotConditionType.BuffTag:
                            PrintWithScope($"Condition: BuffTag " +
                                $"{(condition.Not_condition ? "[Not] " : "[Is]  ")}" +
                                $"{_tags[(uint)condition.Param1].Name}", depth);
                        break;
                        default:
                            PrintWithScope($"Condition[{condition.Id}] Kind={condition.Kind_id} " +
                            (condition.Not_condition ? "[Not] " : "[Is]  ") +
                            $"P1={condition.Param1} " +
                            $"P2={condition.Param2} " +
                            $"P3={condition.Param3}", depth); ;
                        break;
                    }

                    conditioned = true;
                }
            }
            if (!conditioned)
                PrintWithScope("Unconditional", depth);
        }
        private PlotNextEvent GetNextEvent(uint eventId, int pos)
        {
            foreach (var nextEvent in _nextEvents.Values)
                if (nextEvent.EventId == eventId)
                    return nextEvent;
            return null;
        }
        private void AnalyzeEvent(uint eventId, int depth, PlotNextEvent parent)
        {
            //Depth gets fked by loops, this fixes it.
            if (depthFixNeeded)
                depth--;
            depthFixNeeded = false;

            //This is only solution I could find to properly print delay/speed. Big yikes
            if (parent != null)
                PrintWithScope($"Delay:{parent?.Delay} Speed:{parent?.Speed} Failed:{parent?.Fail}", _previousDepth);

            //Print out conditions/effects of current event
            Console.WriteLine();
            PrintWithScope($"EventId: {eventId}", depth);
            AnalyzeConditions(eventId, depth);
            AnalyzeEffects(eventId, depth);
            _previousDepth = depth;

            //Add scope depth if the event is conditional
            if (IsConditioned(eventId) && _previousEvent != eventId)
                depth++;

            //Increment Event Ticket
            if (_tickets.ContainsKey(eventId))
                _tickets[eventId]++;
            else
                _tickets.Add(eventId, 1);

            //Check if we hit max recursion/tickets
            if (_tickets[eventId] > _events[eventId].Tickets)
            {
                if (_events[eventId].Tickets > 1)
                {
                    if (_previousEvent == eventId)
                        depthFixNeeded = true;
                    PrintWithScope($"Event {eventId}: Looped {_tickets[eventId] - 1} times.\n", depth);

                    //I think we only break scope if tickets > 1.. Might be wrong though..
                    return;
                }
            }

            _previousEvent = eventId;

            List<PlotNextEvent> nextEvents = new List<PlotNextEvent>();

            //Gather all event steps into a list
            foreach (var plotEvent in _nextEvents.Values)
                if (plotEvent.EventId == eventId)
                    nextEvents.Add(plotEvent);
            //Very important we sort by position order!!
            nextEvents = nextEvents.OrderBy(o => o.Position).ToList();

            //This handles event steps. Some recursion magic
            //Leaves scope when no steps left with nextIds
            foreach (var nextEvent in nextEvents)
            {
                AnalyzeEvent(nextEvent.NextEventId, depth, nextEvent);
            }
        }

        public void AnalyzePlot(uint plotId)
        {
            //reset tickets
            _tickets = new Dictionary<uint, uint>();

            Plot plot = _plots[plotId];
            Stopwatch ts = new Stopwatch();

            ts.Start();
            AnalyzeEvent(plot.StartEventId, 0, null);
            ts.Stop();

            Console.WriteLine($"It took {ts.ElapsedMilliseconds}ms to analyze the plot with output.");
        }

        public void LoadDb(string dbLocation)
        {
            using (var connection = new SQLiteConnection(dbLocation))
            {
                connection.Open();

                #region PlotEvents
                var command = connection.CreateCommand();
                command.CommandText =
                "SELECT id, tickets FROM plot_events";

                using (var reader = command.ExecuteReader())
                {
                    _events = new Dictionary<uint, PlotEvent>();
                    while (reader.Read())
                    {
                        PlotEvent plotEvent = new PlotEvent();
                        plotEvent.Id = (uint)reader.GetInt32(0);
                        plotEvent.Tickets = reader.GetInt32(1);

                        _events.Add(plotEvent.Id, plotEvent);
                    }
                }
                #endregion

                #region PlotConditions
                command = connection.CreateCommand();
                command.CommandText =
                @"SELECT
                plot_event_conditions.event_id as event_id,
                plot_event_conditions.position as position,
                plot_conditions.id as condition_id,
                plot_conditions.not_condition as not_condition,
                plot_conditions.kind_id as kind_id,
                plot_conditions.param1,
                plot_conditions.param2,
                plot_conditions.param3
                FROM plot_event_conditions
                LEFT JOIN plot_conditions
                ON plot_event_conditions.condition_id = plot_conditions.id";

                using (var reader = command.ExecuteReader())
                {
                    _conditions = new Dictionary<uint, PlotCondition>();
                    while (reader.Read())
                    {
                        PlotCondition condition = new PlotCondition();
                        condition.EventId = (uint)reader.GetInt32(0);
                        condition.Position = reader.GetInt32(1);
                        condition.Id = (uint)reader.GetInt32(2);
                        condition.Not_condition = reader.GetString(3)[0] == 't';
                        //condition.Not_condition = reader.GetInt32(3) == 1;
                        condition.Kind_id = (PlotConditionType)reader.GetInt32(4);
                        condition.Param1 = reader.GetInt32(5);
                        condition.Param2 = reader.GetInt32(6);
                        condition.Param3 = reader.GetInt32(7);

                        _conditions.Add(condition.Id, condition);
                    }
                }
                #endregion

                #region PlotNextEvents
                command = connection.CreateCommand();
                command.CommandText =
                "SELECT id, event_id, position, next_event_id, delay, speed, fail FROM plot_next_events";

                using (var reader = command.ExecuteReader())
                {
                    _nextEvents = new Dictionary<uint, PlotNextEvent>();
                    while (reader.Read())
                    {
                        PlotNextEvent nextEvent = new PlotNextEvent();
                        nextEvent.Id = (uint)reader.GetInt32(0);
                        nextEvent.EventId = (uint)reader.GetInt32(1);
                        nextEvent.Position = reader.GetInt32(2);
                        nextEvent.NextEventId = (uint)reader.GetInt32(3);
                        nextEvent.Delay = reader.GetInt32(4);
                        nextEvent.Speed = reader.GetInt32(5);
                        nextEvent.Fail = reader.GetString(6)[0] == 't';
                        //nextEvent.Fail = reader.GetInt32(6) == 1;

                        _nextEvents.Add(nextEvent.Id, nextEvent);
                    }
                }
                #endregion

                #region Plots
                command = connection.CreateCommand();
                command.CommandText =
                @"SELECT plot_id, id FROM plot_events WHERE position = 1";

                using (var reader = command.ExecuteReader())
                {
                    _plots = new Dictionary<uint, Plot>();
                    while (reader.Read())
                    {
                        Plot plot = new Plot();
                        plot.Id = (uint)reader.GetInt32(0);
                        plot.StartEventId = (uint)reader.GetInt32(1);

                        _plots.Add(plot.Id, plot);
                    }
                }
                #endregion

                #region Effects
                command = connection.CreateCommand();
                command.CommandText =
                @"SELECT id, event_id, position, actual_id, actual_type FROM plot_effects";

                using (var reader = command.ExecuteReader())
                {
                    _effects = new Dictionary<uint, PlotEffect>();
                    while (reader.Read())
                    {
                        PlotEffect effect = new PlotEffect();
                        effect.Id = (uint)reader.GetInt32(0);
                        effect.EventId = (uint)reader.GetInt32(1);
                        effect.Position = reader.GetInt32(2);
                        effect.ActualId = (uint)reader.GetInt32(3);
                        effect.ActualType = reader.GetString(4);

                        _effects.Add(effect.Id, effect);
                    }
                }
                #endregion

                #region Translations
                command = connection.CreateCommand();
                command.CommandText =
                "SELECT idx, en_us FROM localized_texts " +
                "WHERE " +
                "tbl_name = 'tags' " +
                "AND tbl_column_name = 'name'";

                using (var reader = command.ExecuteReader())
                {
                    _tags = new Dictionary<uint, Tag>();
                    while (reader.Read())
                    {
                        Tag tag = new Tag();
                        tag.Id = (uint)reader.GetInt32(0);
                        tag.Name = reader.GetString(1);

                        _tags.Add(tag.Id, tag);
                    }
                }

                command = connection.CreateCommand();
                command.CommandText =
                @"  SELECT
                    buff_effects.id,
                    localized_texts.en_us
                    FROM buff_effects
                    LEFT JOIN localized_texts
                    ON buff_effects.buff_id = localized_texts.idx
                    WHERE localized_texts.tbl_name = 'buffs'
                    AND localized_texts.tbl_column_name = 'name'";

                using (var reader = command.ExecuteReader())
                {
                    _buffEffects = new Dictionary<uint, BuffEffect>();
                    while (reader.Read())
                    {
                        BuffEffect buffEffect = new BuffEffect();
                        buffEffect.Id = (uint)reader.GetInt32(0);
                        buffEffect.Name = reader.GetString(1);

                        _buffEffects.Add(buffEffect.Id, buffEffect);
                    }
                }
                #endregion
            }
        }
    }
}
