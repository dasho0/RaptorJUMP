using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class AccelerationMoveProvider : ContinuousMoveProvider
{
    [SerializeField] private float baseAcceleration = 0.2f;
    [SerializeField] private float speedCap = 5f;
    [SerializeField] private GravityProvider gravityProvider;
    [SerializeField, Range(0.25f, 2f)]
    private float shapeExponent = 1.0f;
    [SerializeField]
    private float normalizeAtTime = 4f;

    private float currentSpeed = 0f;

private float AccelerateFromCurrent(float s) {
    var k = baseAcceleration;
    var cap = speedCap;
    var alpha = shapeExponent;
    var T = normalizeAtTime;

    var denom = 1f - Mathf.Exp(-k * Mathf.Pow(T, alpha));

    var A = Mathf.Clamp01((s / cap) * denom);
    var tPowA = -Mathf.Log(1f - A) / k;
    var t = Mathf.Pow(tPowA, 1f / alpha);

    var tNext = t + Time.deltaTime;
    var sNext = cap * (1f - Mathf.Exp(-k * Mathf.Pow(tNext, alpha))) / denom;

    return Mathf.Min(sNext, cap);
}

    protected override Vector3 ComputeDesiredMove(Vector2 input) {
        var shouldAccelerate = gravityProvider.isGrounded;
        // TODO: this should be handled properly probably

        if(input == Vector2.zero) {
            currentSpeed = 0f;
            return Vector3.zero;
        }

        var xrOrigin = mediator.xrOrigin;
        if (xrOrigin == null)
            return Vector3.zero;

        // Assumes that the input axes are in the range [-1, 1].
        // Clamps the magnitude of the input direction to prevent faster speed when moving diagonally,
        // while still allowing for analog input to move slower (which would be lost if simply normalizing).
        var inputMove = Vector3.ClampMagnitude(new Vector3(base.enableStrafe ? input.x : 0f, 0f, input.y), 1f);

        var deltaTime = Time.deltaTime;

        // Determine frame of reference for what the input direction is relative to
        var forwardSourceTransform = forwardSource == null ? xrOrigin.Camera.transform : forwardSource;
        var inputForwardInWorldSpace = forwardSourceTransform.forward;

        if(shouldAccelerate) {
            currentSpeed = AccelerateFromCurrent(currentSpeed);
        }

        var originTransform = xrOrigin.Origin.transform;
        var speedFactor = currentSpeed * deltaTime * originTransform.localScale.x; // Adjust speed with user scale

        // If flying, just compute move directly from input and forward source
        if(enableFly) {
            var inputRightInWorldSpace = forwardSourceTransform.right;
            var combinedMove = inputMove.x * inputRightInWorldSpace + inputMove.z * inputForwardInWorldSpace;
            return combinedMove * speedFactor;
        }

        var originUp = originTransform.up;

        if(Mathf.Approximately(Mathf.Abs(Vector3.Dot(inputForwardInWorldSpace, originUp)), 1f)) {
            // When the input forward direction is parallel with the rig normal,
            // it will probably feel better for the player to move along the same direction
            // as if they tilted forward or up some rather than moving in the rig forward direction.
            // It also will probably be a better experience to at least move in a direction
            // rather than stopping if the head/controller is oriented such that it is perpendicular with the rig.
            inputForwardInWorldSpace = -forwardSourceTransform.up;
        }

        var inputForwardProjectedInWorldSpace = Vector3.ProjectOnPlane(inputForwardInWorldSpace, originUp);
        var forwardRotation = Quaternion.FromToRotation(originTransform.forward, inputForwardProjectedInWorldSpace);

        var rigSpaceDir = new Vector3(inputMove.x, 0f, inputMove.z).normalized;
        var translationInRigSpace = forwardRotation * rigSpaceDir * speedFactor;
        var translationInWorldSpace = originTransform.TransformDirection(translationInRigSpace);

        return translationInWorldSpace;
    }
}
