using System.Collections.Generic;
using UnityEngine;

namespace Game.Story
{
    public sealed class CaseBoard : MonoBehaviour
    {
        [SerializeField] private List<CaseFileDataSO> caseFiles = new();

        public IReadOnlyList<CaseFileDataSO> CaseFiles => caseFiles;

        public bool TryGetFirstUnlockedCase(out CaseFileDataSO caseFile)
        {
            for (int i = 0; i < caseFiles.Count; i++)
            {
                CaseFileDataSO candidate = caseFiles[i];
                if (candidate != null && candidate.Unlocked)
                {
                    caseFile = candidate;
                    return true;
                }
            }

            caseFile = null;
            return false;
        }
    }
}
