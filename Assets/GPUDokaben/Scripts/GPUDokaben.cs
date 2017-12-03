using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Dokaben
{
    public class GPUDokaben : MonoBehaviour
    {
        // ==============================
        #region // Defines

        /// <summary>
        /// スレッドグループのスレッドサイズ
        /// </summary>
        const int ThreadBlockSize = 256;

        /// <summary>
        /// 2x2の行列
        /// </summary>
        /// <remarks>Unityが用意している物が4x4しか無い上に、そんなに必要無いので用意</remarks>
        public struct Matrix2x2
        {
            public float m00;
            public float m11;
            public float m01;
            public float m10;
            /// <summary>
            /// 単位行列
            /// </summary>
            public static Matrix2x2 identity
            {
                get
                {
                    var m = new Matrix2x2();
                    m.m00 = m.m11 = 1f;
                    m.m01 = m.m10 = 0f;
                    return m;
                }
            }
        }

        /// <summary>
        /// ドカベンロゴのデータ
        /// </summary>
        struct DokabenData
        {
            /// <summary>
            /// 座標
            /// </summary>
            public Vector3 Position;
            /// <summary>
            /// 回転
            /// </summary>
            public Matrix2x2 Rotation;
        }

        #endregion // Defines

        // ==============================
        #region // Serialize Fields

        /// <summary>
        /// 最大オブジェクト数
        /// </summary>
        /// <remarks>2^8 ~ 2^15</remarks>
        [Range(256, 32768)]
        [SerializeField]
        int _MaxObjectNum = 14384;

        /// <summary>
        /// ComputeShaderの参照
        /// </summary>
        [SerializeField] ComputeShader _ComputeShader;

        /// <summary>
        /// ドカベンのMesh
        /// </summary>
        [SerializeField] Mesh _DokabenMesh;

        /// <summary>
        /// ドカベンのMaterial
        /// </summary>
        [SerializeField] Material _DokabenMaterial;

        /// <summary>
        /// ドカベンMeshのScale(サイズ固定)
        /// </summary>
        [SerializeField] Vector3 _DokabenMeshScale = new Vector3(1f, 1f, 1f);

        /// <summary>
        /// アニメーション速度
        /// </summary>
        [SerializeField] float _AnimationSpeed = 1f;

        /// <summary>
        /// 表示領域の中心座標
        /// </summary>
        [SerializeField] Vector3 _BoundCenter = Vector3.zero;

        /// <summary>
        /// 表示領域のサイズ
        /// </summary>
        [SerializeField] Vector3 _BoundSize = new Vector3(32f, 32f, 32f);

        #endregion // Serialize Fields

        // ==============================
        #region // Private Fields

        /// <summary>
        /// ドカベンロゴのバッファ
        /// </summary>
        ComputeBuffer _DokabenDataBuffer;

        /// <summary>
        /// アニメーションの開始位置のバッファ
        /// </summary>
        ComputeBuffer _AnimationStartPositionBuffer;

        /// <summary>
        /// GPU Instancingの為の引数
        /// </summary>
        uint[] _GPUInstancingArgs = new uint[5] { 0, 0, 0, 0, 0 };

        /// <summary>
        /// GPU Instancingの為の引数バッファ
        /// </summary>
        ComputeBuffer _GPUInstancingArgsBuffer;

        #endregion // Private Fields


        // --------------------------------------------------
        #region // MonoBehaviour Methods

        /// <summary>
        /// 初期化
        /// </summary>
        void Start()
        {
            // バッファ生成
            this._DokabenDataBuffer = new ComputeBuffer(this._MaxObjectNum, Marshal.SizeOf(typeof(DokabenData)));
            this._AnimationStartPositionBuffer = new ComputeBuffer(this._MaxObjectNum, Marshal.SizeOf(typeof(float)));
            this._GPUInstancingArgsBuffer = new ComputeBuffer(1, this._GPUInstancingArgs.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            var rotationMatrixArr = new DokabenData[this._MaxObjectNum];
            var timeArr = new float[this._MaxObjectNum];
            for (int i = 0; i < this._MaxObjectNum; ++i)
            {
                // バッファに初期値を代入
                var halfX = this._BoundSize.x / 2;
                var halfY = this._BoundSize.y / 2;
                var halfZ = this._BoundSize.z / 2;
                rotationMatrixArr[i].Position = new Vector3(
                    Random.Range(-halfX, halfX),
                    Random.Range(-halfY, halfY),
                    Random.Range(-halfZ, halfZ));
                rotationMatrixArr[i].Rotation = Matrix2x2.identity;
                // 0~90度の間でランダムに開始させてみる
                // →同じアニメーションを行うなら0を渡せばok
                timeArr[i] = Random.Range(0f, 90f * Mathf.Deg2Rad);
            }
            this._DokabenDataBuffer.SetData(rotationMatrixArr);
            this._AnimationStartPositionBuffer.SetData(timeArr);
            rotationMatrixArr = null;
            timeArr = null;
        }

        /// <summary>
        /// 毎フレーム更新
        /// </summary>
        void Update()
        {
            // ComputeShader
            int kernelId = this._ComputeShader.FindKernel("MainCS");
            this._ComputeShader.SetFloat("_Time", Time.time);
            this._ComputeShader.SetFloat("_AnimationSpeed", this._AnimationSpeed);
            this._ComputeShader.SetBuffer(kernelId, "_DokabenDataBuffer", this._DokabenDataBuffer);
            this._ComputeShader.SetBuffer(kernelId, "_AnimationStartPositionBuffer", this._AnimationStartPositionBuffer);
            this._ComputeShader.Dispatch(kernelId, (Mathf.CeilToInt(this._MaxObjectNum / ThreadBlockSize) + 1), 1, 1);

            // GPU Instaicing
            this._GPUInstancingArgs[0] = (this._DokabenMesh != null) ? this._DokabenMesh.GetIndexCount(0) : 0;
            this._GPUInstancingArgs[1] = (uint)this._MaxObjectNum;
            this._GPUInstancingArgsBuffer.SetData(this._GPUInstancingArgs);
            this._DokabenMaterial.SetBuffer("_DokabenDataBuffer", this._DokabenDataBuffer);
            this._DokabenMaterial.SetVector("_DokabenMeshScale", this._DokabenMeshScale);
            Graphics.DrawMeshInstancedIndirect(this._DokabenMesh, 0, this._DokabenMaterial, new Bounds(this._BoundCenter, this._BoundSize), this._GPUInstancingArgsBuffer);
        }

        /// <summary>
        /// 破棄
        /// </summary>
        void OnDestroy()
        {
            if (this._DokabenDataBuffer != null)
            {
                this._DokabenDataBuffer.Release();
                this._DokabenDataBuffer = null;
            }
            if (this._AnimationStartPositionBuffer != null)
            {
                this._AnimationStartPositionBuffer.Release();
                this._AnimationStartPositionBuffer = null;
            }
            if (this._GPUInstancingArgsBuffer != null)
            {
                this._GPUInstancingArgsBuffer.Release();
                this._GPUInstancingArgsBuffer = null;
            }
        }

        #endregion // MonoBehaviour Method
    }
}
