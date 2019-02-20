﻿using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Snapshooter.Exceptions;

namespace Snapshooter.Core
{
    /// <summary>
    /// The snapshot comparer is responsible to compare the actual snapshot with the
    /// existing one and also inclode the field match options checks.
    /// </summary>
    public class JsonSnapshotComparer : ISnapshotComparer
    {
        private readonly IAssert _snapshotAssert;
        private readonly ISnapshotSerializer _snapshotSerializer;

        /// <summary>
        /// Creates a new instance of the <see cref="JsonSnapshotComparer"/>
        /// </summary>
        /// <param name="snapshotAssert">The snapshot assert.</param>
        /// <param name="snapshotSerializer">The snapshot serializer.</param>
        public JsonSnapshotComparer(
            IAssert snapshotAssert, ISnapshotSerializer snapshotSerializer)
        {
            _snapshotAssert = snapshotAssert;
            _snapshotSerializer = snapshotSerializer;
        }

        /// <summary>
        /// Compares the current snapshot with the expected snapshot and applies 
        /// the compare rules of the compare actions.
        /// </summary>
        /// <param name="matchOptions">The compare actions, which will be used for special comparion.</param>
        /// <param name="expectedSnapshot">The original snapshot of the current result.</param>
        /// <param name="actualSnapshot">The actual (modifiable) snapshot of the current result.</param>
        public void CompareSnapshots(
            string expectedSnapshot,
            string actualSnapshot,
            Func<MatchOptions, MatchOptions> matchOptions)
        {
            JToken originalActualSnapshotToken = _snapshotSerializer.Deserialize(actualSnapshot);
            JToken actualSnapshotToken = _snapshotSerializer.Deserialize(actualSnapshot);
            JToken expectedSnapshotToken = _snapshotSerializer.Deserialize(expectedSnapshot);

            if (matchOptions != null)
            {
                ExecuteFieldMatchActions(originalActualSnapshotToken,
                    actualSnapshotToken, expectedSnapshotToken, matchOptions);
            }

            string actualSnapshotToCompare = _snapshotSerializer
                .SerializeJsonToken(actualSnapshotToken);
            string expectedSnapshotToCompare = _snapshotSerializer
                .SerializeJsonToken(expectedSnapshotToken);

            _snapshotAssert.Assert(expectedSnapshotToCompare, actualSnapshotToCompare);
        }
        
        private void ExecuteFieldMatchActions(
            JToken originalActualSnapshot,
            JToken actualSnapshot,
            JToken expectedSnapshot,
            Func<MatchOptions, MatchOptions> matchOptions)
        {
            try
            {
                MatchOptions configuredMatchOptions = matchOptions(new MatchOptions());

                foreach (FieldMatchOperator matchOperator in configuredMatchOptions.MatchOperators)
                {
                    FieldOption fieldOption = matchOperator.ExecuteMatch(originalActualSnapshot);

                    RemoveFieldFromSnapshot(fieldOption.FieldPath, actualSnapshot);
                    RemoveFieldFromSnapshot(fieldOption.FieldPath, expectedSnapshot);
                }
            }
            catch (SnapshotFieldException)
            {
                throw;
            }
            catch (Exception err)
            {
                throw new SnapshotCompareException($"The compare action " +
                    $"has been failed. Error: {err.Message}");
            }
        }

        /// <summary>
        ///  Removes a field from the snapshot.
        /// </summary>
        /// <param name="fieldPath">The field path of the field to remove.</param>
        /// <param name="snapshot">The snapshot from which the field shall be removed.</param>
        private static void RemoveFieldFromSnapshot(string fieldPath, JToken snapshot)
        {            
            if (snapshot is JValue jValue)
            {                
                throw new NotSupportedException($"No snapshot match options are " +
                    $"supported for snapshots with scalar values. Therefore the " +
                    $"match option with field '{fieldPath}' is not allowed.");                
            }

            IEnumerable<JToken> actualTokens = snapshot.SelectTokens(fieldPath, false);
            if (actualTokens != null)
            {
                foreach (JToken actual in actualTokens.ToList())
                {
                    if (actual.Parent is JArray)
                    {
                        ((JArray)actual.Parent).Remove(actual);
                    }
                    else
                    {
                        actual.Parent.Remove();
                    }
                }
            }
        }
    }
}
