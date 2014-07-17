using UnityEngine;

namespace UnitySteer.Base
{

/// <summary>
/// Parent class for objects that vehicles can aim for, be it other vehicles or
/// static objects.
/// </summary>
[AddComponentMenu("UnitySteer/Detectables/DetectableObject")]
public class DetectableObject : MonoBehaviour
{
    private Transform _transform;

    [SerializeField]
	protected bool _drawGizmos = false;

	/// <summary>
	/// The vehicle's center in the transform
	/// </summary>
	[SerializeField, HideInInspector]
	Vector3 _center;
	
	/// <summary>
	/// The vehicle's center in the transform, scaled to by the transform's lossyScale
	/// </summary>
	[SerializeField, HideInInspector]
	Vector3 _scaledCenter;
	
	/// <summary>
	/// The vehicle's radius, scaled by the maximum of the transform's lossyScale values
	/// </summary>
	[SerializeField, HideInInspector]
	float _scaledRadius = 1;

	/// <summary>
	/// The vehicle's radius.
	/// </summary>
	[SerializeField, HideInInspector]
	float _radius = 1;
	

	
	/// <summary>
	/// Vehicle's position
	/// </summary>
	/// <remarks>The vehicle's position is the transform's position displaced 
	/// by the vehicle center</remarks>
	public Vector3 Position {
	    get { return Transform.position + _scaledCenter; }
	}
	
	/// <summary>
	/// Vehicle center on the transform
	/// </summary>
	/// <remarks>
	/// This property's setter recalculates a temporary value, so it's
	/// advised you don't re-scale the vehicle's transform after it has been set
	/// </remarks>
	public Vector3 Center 
    {
		get { return _center; }
		set 
        {
			_center = value;
			RecalculateScaledValues();
		}
	}
	
	/// <summary>
	/// Vehicle radius
	/// </summary>
	/// <remarks>
	/// This property's setter recalculates a temporary value, so it's
	/// advised you don't re-scale the vehicle's transform after it has been set
	/// </remarks>
	public float Radius 
    {
		get { return _radius; }
		set 
        {
			_radius = Mathf.Clamp(value, 0.01f, float.MaxValue);
			RecalculateScaledValues();			
		}
	}

	/// <summary>
	/// The vehicle's center in the transform, scaled to by the transform's lossyScale
	/// </summary>
	public Vector3 ScaledCenter 
    {
		get { return _scaledCenter; }
	}
	
	/// <summary>
	/// The vehicle's radius, scaled by the maximum of the transform's lossyScale values
	/// </summary>
	public float ScaledRadius 
    {
		get { return _scaledRadius; }
	}

    /// <summary>
    /// Calculated squared object radius
    /// </summary>
    public float SquaredRadius { get; private set; }

    /// <summary>
    /// Cached transform for this behaviour
    /// </summary>
    public Transform Transform
    {
        get { return _transform ?? (_transform = transform); }
    }
	
	#region Methods
	protected virtual void Awake()
	{
		if (collider && Transform.parent != null)
		{
			Debug.LogWarning(string.Format("DetectableObject should be on the transform root. Unparenting {0}", this));
			Transform.parent = null;
		}

		RecalculateScaledValues();
	}
	
	/// <summary>
	/// Predicts where the vehicle will be at a point in the future
	/// </summary>
	/// <param name="predictionTime">
	/// A time in seconds for the prediction <see cref="System.Single"/>. 
	/// Disregarded on the base function since obstacles do not move.
	/// </param>
	/// <returns>
	/// Object position<see cref="Vector3"/>
	/// </returns>
	public virtual Vector3 PredictFuturePosition(float predictionTime)
    {
        return Transform.position;
	}
	
	
	/// <summary>
	/// Recalculates the object's scaled radius and center
	/// </summary>
	protected virtual void RecalculateScaledValues() 
    {
		var scale  = Transform.lossyScale;
		_scaledRadius = _radius * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
		_scaledCenter = Vector3.Scale(_center, scale);
		SquaredRadius = _scaledRadius * _scaledRadius;
	}
	
	protected virtual void OnDrawGizmos()
	{
	    if (!_drawGizmos) return;
	    Gizmos.color = Color.blue;
	    Gizmos.DrawWireSphere(Position, ScaledRadius);
	}
	#endregion
}

}

