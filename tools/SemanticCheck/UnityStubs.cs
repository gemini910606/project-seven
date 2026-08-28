// Hand-written stand-ins for the Unity, Netcode and Input System API surface
// this project uses, so that every runtime script can be type-checked without an
// editor or a licence.
//
// WHAT A GREEN BUILD PROVES
//   Every cross-file reference inside Game.* lines up: no method called by a name
//   it does not have, no wrong argument count, no type that cannot flow where it
//   is sent. That is the failure mode this project keeps producing - two correct
//   pieces that were never actually connected.
//
// WHAT IT DOES NOT PROVE
//   That the code compiles in Unity. These stubs are written from memory and are
//   certainly wrong in places. A stub whose shape is wrong in the same direction
//   as a call site will pass here and fail in the editor. Only Unity can settle
//   that, and unity-tests.yml is where it happens.
//
// WHEN THIS GOES RED FOR A STUB REASON
//   Using a Unity API that is not stubbed yet fails the build with CS0246 or
//   CS1061 naming a UnityEngine type. That is not a bug in your change - add the
//   member here in the same commit. An error naming a Game.* symbol is real.

using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object { public string name; public static void Destroy(Object o, float t = 0f) {} public static void DestroyImmediate(Object o) {} public static T Instantiate<T>(T o) where T : Object => o; public static GameObject Instantiate(GameObject o, Vector3 p, Quaternion r, Transform parent = null) => o; public static T FindFirstObjectByType<T>() where T : Object => null; }

    public class Component : Object
    {
        public GameObject gameObject; public Transform transform;
        public T GetComponent<T>() => default; public T GetComponentInParent<T>() => default;
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) => new T[0];
        public bool TryGetComponent<T>(out T c) { c = default; return false; }
    }

    public class Behaviour : Component { public bool enabled; public bool isActiveAndEnabled; }

    public class MonoBehaviour : Behaviour
    {
        public void Invoke(string m, float t) {} public void CancelInvoke(string m) {} public void CancelInvoke() {}
        public Coroutine StartCoroutine(IEnumerator r) => null; public void StopAllCoroutines() {}
    }
    public class Coroutine {}

    public class GameObject : Object
    {
        public GameObject() {} public GameObject(string n) { name = n; }
        public Transform transform; public int layer; public string tag; public bool activeSelf;
        public void SetActive(bool v) {}
        public T GetComponent<T>() => default; public T GetComponentInParent<T>() => default;
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) => new T[0];
        public bool TryGetComponent<T>(out T c) { c = default; return false; }
        public T AddComponent<T>() where T : Component => default;
    }

    public class Transform : Component
    {
        public Vector3 position, localPosition, localScale, forward, right, up;
        public Quaternion rotation, localRotation; public Transform parent; public int childCount; public Vector3 eulerAngles, localEulerAngles; public Matrix4x4 localToWorldMatrix;
        public void SetParent(Transform p) {} public void SetPositionAndRotation(Vector3 p, Quaternion r) {}
        public Transform Find(string n) => null; public Transform GetChild(int i) => null;
        public Vector3 TransformDirection(Vector3 v) => v; public Vector3 InverseTransformPoint(Vector3 v) => v;
    }

    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero; public float magnitude => 0f; public float sqrMagnitude => 0f; public Vector2 normalized => this;
        public static Vector2 operator +(Vector2 a, Vector2 b) => a; public static Vector2 operator -(Vector2 a, Vector2 b) => a;
        public static Vector2 operator *(Vector2 a, float b) => a; public static Vector2 operator *(float b, Vector2 a) => a;
        public static Vector2 operator /(Vector2 a, float b) => a;
        public static float Dot(Vector2 a, Vector2 b) => 0f; public static Vector2 ClampMagnitude(Vector2 a, float m) => a;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a; public static Vector2 MoveTowards(Vector2 a, Vector2 b, float t) => a; }

    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero, one, up, down, forward, back, left, right;
        public float magnitude => 0f; public float sqrMagnitude => 0f; public Vector3 normalized => this;
        public static Vector3 operator +(Vector3 a, Vector3 b) => a; public static Vector3 operator -(Vector3 a, Vector3 b) => a;
        public static Vector3 operator -(Vector3 a) => a;
        public static Vector3 operator *(Vector3 a, float b) => a; public static Vector3 operator *(float b, Vector3 a) => a;
        public static Vector3 operator /(Vector3 a, float b) => a;
        public static float Distance(Vector3 a, Vector3 b) => 0f; public static float Dot(Vector3 a, Vector3 b) => 0f;
        public static Vector3 Cross(Vector3 a, Vector3 b) => a; public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a;
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float t) => a; public static Vector3 ClampMagnitude(Vector3 a, float m) => a;
        public static Vector3 ProjectOnPlane(Vector3 a, Vector3 n) => a; public static Vector3 Normalize(Vector3 a) => a; public void Normalize() {}
        public static Vector3 SmoothDamp(Vector3 a, Vector3 b, ref Vector3 v, float t) => a;
        public static float Angle(Vector3 a, Vector3 b) => 0f; public static Vector3 Scale(Vector3 a, Vector3 b) => a; }

    public struct Quaternion { public static Quaternion identity;
        public static Quaternion Euler(float x, float y, float z) => identity; public static Quaternion Euler(Vector3 v) => identity;
        public static Quaternion LookRotation(Vector3 f) => identity; public static Quaternion LookRotation(Vector3 f, Vector3 u) => identity;
        public static Quaternion AngleAxis(float a, Vector3 x) => identity; public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => a;
        public static Quaternion RotateTowards(Quaternion a, Quaternion b, float t) => a;
        public Vector3 eulerAngles; public static Vector3 operator *(Quaternion q, Vector3 v) => v; }

    public static class Mathf { public const float Infinity = 1f, PI = 3.14159f, Deg2Rad = 1f, Rad2Deg = 1f, Epsilon = 1e-5f;
        public static float Min(float a, float b) => a; public static int Min(int a, int b) => a;
        public static float Max(float a, float b) => a; public static int Max(int a, int b) => a;
        public static float Clamp(float v, float a, float b) => v; public static int Clamp(int v, int a, int b) => v;
        public static float Clamp01(float v) => v; public static float Abs(float v) => v; public static float Sqrt(float v) => v;
        public static float Sin(float v) => v; public static float Cos(float v) => v; public static float Tan(float v) => v;
        public static float Atan(float v) => v; public static float Acos(float v) => v; public static float Exp(float v) => v;
        public static float Pow(float a, float b) => a; public static float Lerp(float a, float b, float t) => a;
        public static float MoveTowards(float a, float b, float t) => a; public static float Repeat(float a, float b) => a;
        public static float DeltaAngle(float a, float b) => a; public static int RoundToInt(float v) => 0; public static int FloorToInt(float v) => 0;
        public static float SmoothDamp(float a, float b, ref float v, float t) => a;
        public static float SmoothDamp(float a, float b, ref float v, float t, float max, float dt) => a; }

    public static class Time { public static float time, deltaTime, fixedDeltaTime, unscaledTime; }
    public static class Debug { public static void Log(object m, Object c = null) {} public static void LogWarning(object m, Object c = null) {} public static void LogError(object m, Object c = null) {} public static void DrawLine(Vector3 a, Vector3 b, Color c, float d = 0f) {} }
    public static class Random { public static float value; public static float Range(float a, float b) => a; public static int Range(int a, int b) => a; public static Vector3 insideUnitSphere; public static Vector2 insideUnitCircle; }

    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }
    public struct RaycastHit { public Vector3 point, normal; public float distance; public Collider collider; public Transform transform; }
    public static class Physics {
        public static bool Raycast(Vector3 o, Vector3 d, out RaycastHit h, float m, int mask, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) { h = default; return false; }
        public static bool Raycast(Vector3 o, Vector3 d, float m, int mask, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) => false;
        public static bool Linecast(Vector3 a, Vector3 b, int mask) => false;
        public static bool SphereCast(Vector3 o, float r, Vector3 d, out RaycastHit h, float m, int mask, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal) { h = default; return false; }
        public static Collider[] OverlapSphere(Vector3 p, float r, int mask) => new Collider[0];
        public static void IgnoreLayerCollision(int a, int b, bool ignore) {} }

    public struct Bounds { public Vector3 center, size, extents; public bool Contains(Vector3 p) => false; }
    public class Collider : Component { public bool enabled, isTrigger; public Bounds bounds; public Rigidbody attachedRigidbody; }
    public class BoxCollider : Collider { public Vector3 center, size; }
    public class SphereCollider : Collider { public Vector3 center; public float radius; }
    public class CapsuleCollider : Collider { public Vector3 center; public float radius, height; }
    public class Rigidbody : Component {}
    public class CharacterController : Collider { public float height, radius, slopeLimit, stepOffset, skinWidth; public Vector3 center, velocity; public bool isGrounded; public void Move(Vector3 m) {} }

    public struct LayerMask { public int value; public static int NameToLayer(string n) => 0; public static string LayerToName(int l) => ""; public static int GetMask(params string[] n) => 0;
        public static implicit operator int(LayerMask m) => m.value; public static implicit operator LayerMask(int v) => new LayerMask { value = v }; }

    public struct Color { public Color(float r, float g, float b, float a = 1f) {} public static Color white, red, green, blue, yellow; }
    public static class Gizmos { public static Color color; public static Matrix4x4 matrix; public static void DrawWireSphere(Vector3 c, float r) {} public static void DrawLine(Vector3 a, Vector3 b) {} public static void DrawCube(Vector3 c, Vector3 s) {} public static void DrawWireCube(Vector3 c, Vector3 s) {} public static void DrawRay(Vector3 o, Vector3 d) {} }
    public struct Matrix4x4 {}
    public enum CursorLockMode { None, Locked, Confined }
    public static class Cursor { public static CursorLockMode lockState; public static bool visible; }

    public class AudioClip : Object {}
    public class AudioSource : Component { public void PlayOneShot(AudioClip c) {} public void Play() {} public AudioClip clip; }
    public class AnimationCurve : Object { public float Evaluate(float t) => 0f; public static AnimationCurve Linear(float a, float b, float c, float d) => null; public static AnimationCurve EaseInOut(float a, float b, float c, float d) => null; }
    public class ScriptableObject : Object { public static T CreateInstance<T>() where T : ScriptableObject => null; }

    public class SerializeFieldAttribute : Attribute {}
    public class HeaderAttribute : Attribute { public HeaderAttribute(string h) {} }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string t) {} }
    public class MinAttribute : Attribute { public MinAttribute(float m) {} }
    public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) {} }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RequireComponentAttribute : Attribute { public RequireComponentAttribute(Type t) {} }
    public class DisallowMultipleComponentAttribute : Attribute {}
    public class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
}

namespace UnityEngine.AI
{
    public struct NavMeshHit { public Vector3 position; }
    public static class NavMesh { public const int AllAreas = -1; public static bool SamplePosition(Vector3 p, out NavMeshHit h, float d, int areas) { h = default; return false; } }
    public class NavMeshAgent : Behaviour { public float radius, height, speed, angularSpeed, acceleration, stoppingDistance, remainingDistance;
        public bool updateRotation, updatePosition, isOnNavMesh, pathPending, isStopped, hasPath;
        public Vector3 velocity, destination, nextPosition, steeringTarget;
        public bool SetDestination(Vector3 t) => false; public bool Warp(Vector3 p) => false; public void ResetPath() {} public void Move(Vector3 m) {} }
}

namespace UnityEngine.InputSystem
{
    public enum InputActionType { Value, Button, PassThrough }
    public class InputAction { public InputAction(string n = null, InputActionType t = InputActionType.Value, string binding = null) {}
        public string name; public event Action<CallbackContext> performed, canceled, started;
        public void AddBinding(string p) {} public void Enable() {} public void Disable() {} public void Dispose() {}
        public bool IsPressed() => false; public bool WasPressedThisFrame() => false; public bool WasReleasedThisFrame() => false;
        public T ReadValue<T>() => default;
        public CompositeSyntax AddCompositeBinding(string c) => default;
        public struct CompositeSyntax { public CompositeSyntax With(string name, string binding) => this; }
        public struct CallbackContext { public T ReadValue<T>() => default; public InputControl control => null; } }
    public class InputControl { public InputDevice device; }
    public class InputDevice {}
    public class Gamepad : InputDevice {}
    public class Keyboard : InputDevice {}
    public class Mouse : InputDevice {}
}

namespace Unity.Netcode
{
    using UnityEngine;
    public enum SendTo { Server, Everyone, ClientsAndHost, NotServer, Owner, NotOwner }
    public class RpcAttribute : Attribute { public RpcAttribute(SendTo t) {} }
    public class ClientRpcAttribute : Attribute {}
    public class ServerRpcAttribute : Attribute { public bool RequireOwnership; }
    public struct RpcParams { public ReceiveParams Receive; public struct ReceiveParams { public ulong SenderClientId; } }

    public class NetworkObject : Component { public bool IsSpawned; public ulong NetworkObjectId; public void Spawn(bool destroyWithScene = false) {} public void Despawn(bool destroy = true) {} }
    public class NetworkClient { public NetworkObject PlayerObject; }
    public class NetworkManager : MonoBehaviour { public static NetworkManager Singleton;
        public bool IsServer, IsClient, IsHost, IsListening; public ulong LocalClientId;
        public IReadOnlyDictionary<ulong, NetworkClient> ConnectedClients; }

    public class NetworkVariable<T> { public NetworkVariable() {} public NetworkVariable(T value) {} public T Value { get; set; }
        public event Action<T, T> OnValueChanged; }

    public class NetworkBehaviour : MonoBehaviour
    {
        public bool IsServer, IsClient, IsHost, IsOwner, IsSpawned;
        public ulong OwnerClientId, NetworkObjectId;
        public NetworkObject NetworkObject; public NetworkManager NetworkManager;
        public virtual void OnNetworkSpawn() {} public virtual void OnNetworkDespawn() {}
        public virtual void OnDestroy() {}
        public new T FindFirstObjectByType<T>() where T : UnityEngine.Object => null;
    }
}

namespace Unity.Netcode.Components { public class NetworkTransform : Unity.Netcode.NetworkBehaviour {} }
