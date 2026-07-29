using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Samirin33.NDMF.Base;

namespace Samirin33.NDMF.Module
{
    public class ModuleParamInfo : SamirinMABase
    {
        [System.Serializable]
        public class ParamInfo
        {
            public string paramName;
            public AnimatorControllerParameterType paramType;
            public string paramExplanation;
            [Tooltip("Float型のデフォルト値（Animatorに既存の場合は自動取得）")]
            public float defaultFloat;
            [Tooltip("Int型のデフォルト値（Animatorに既存の場合は自動取得）")]
            public int defaultInt;
            [Tooltip("Bool型のデフォルト値（Animatorに既存の場合は自動取得）")]
            public bool defaultBool;
        }

        [Tooltip("同じオブジェクトのAnimatorを参照します。未設定の場合は自動で取得を試みます。")]
        public Animator animator;

        public ParamInfo[] paramInfos;

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        public override void OnBuild(SamirinBuildPhase buildPhase, bool beforeModularAvatar, GameObject avatarRootObject)
        {
            if (buildPhase == SamirinBuildPhase.Transforming && beforeModularAvatar)
            {
                DestroyImmediate(this);
            }
        }
    }
}