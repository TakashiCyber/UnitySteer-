// ----------------------------------------------------------------------------
//
// Ported to Unity by Ricardo J. Méndez http://www.arges-systems.com/
//
// OpenSteer - pure .net port
// Port by Simon Oliver - http://www.handcircus.com
//
// OpenSteer -- Steering Behaviors for Autonomous Characters
//
// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Original author: Craig Reynolds <craig_reynolds@playstation.sony.com>
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//
//
// ----------------------------------------------------------------------------
#define DEBUG
using System;
using System.Collections;
using System.Text;
using UnityEngine;

namespace UnitySteer
{
    public class SimpleVehicle : SteerLibrary
    {

        ArrayList obstacles;    // Obstacle list
//        float _mass;       // mass (defaults to unity so acceleration=force)
//        float _radius;     // size of bounding sphere, for obstacle avoidance, etc.
//        float _speed;      // speed along Forward direction.  Because local space
                           // is velocity-aligned, velocity = Forward * Speed

        //float maxForce = 27f;   // the maximum steering force this vehicle can apply
                                // (steering force is clipped to this magnitude)

        float _curvature;
        Vector3 _lastForward;
        Vector3 _lastPosition;
        Vector3 _smoothedPosition;
        float _smoothedCurvature;
        Vector3 _smoothedAcceleration;
        
        public int SerialNumber
        {
            get
            {
                return GameObject.GetInstanceID();
            }
        }

        
        public SimpleVehicle( Vector3 position, float mass ) : base( position, mass )
        {
            // set inital state
            reset();
        }
		
		public SimpleVehicle( Transform transform, float mass ) : base( transform, mass )
		{
            // set inital state
            reset();
		}
		
		
		public SimpleVehicle( Rigidbody rigidbody ) : base( rigidbody )
		{
            // set inital state
            reset();
		}


        // Reset vehicle state
        public virtual void reset()
        {
            // reset SteerLibraryMixin state
            // (XXX this seems really fragile, needs to be redesigned XXX)
            //SimpleVehicle_3.reset ();
            resetSteering();

            // TODO-RJM: This reset thing sucks
            Mass = 1.0f;        // mass (defaults to 1 so acceleration=force)
            Speed = 0.0f;       // speed along Forward direction.

            Radius = 0.5f;      // size of bounding sphere

            MaxForce = 0.1f;    // steering force is clipped to this magnitude
            MaxSpeed = 1.0f;    // velocity is clipped to this magnitude

            // reset bookkeeping to do running averages of these quanities
            resetSmoothedPosition (Vector3.zero);
            resetSmoothedCurvature(0);//Vector3.zero);
            resetSmoothedAcceleration(Vector3.zero);
        }
        
        public ArrayList Obstacles
        {
            get
            {
                return obstacles;
            }
            set
            {
                obstacles = value;
            }
        }
        
        
        // get instantaneous curvature (since last update)
        float curvature () {return _curvature;}

        // get/reset smoothedCurvature, smoothedAcceleration and smoothedPosition
        float smoothedCurvature () {return _smoothedCurvature;}
        float resetSmoothedCurvature (float value)
        {
            _lastForward = Vector3.zero;
            _lastPosition = Vector3.zero;;
            return _smoothedCurvature = _curvature = value;
        }
        Vector3 smoothedAcceleration () {return _smoothedAcceleration;}
        Vector3 resetSmoothedAcceleration (Vector3 value)
        {
            return _smoothedAcceleration = value;
        }
        Vector3 smoothedPosition () {return _smoothedPosition;}
        Vector3 resetSmoothedPosition (Vector3 value)
        {
            return _smoothedPosition = value;
        }


        // ----------------------------------------------------------------------------
        // adjust the steering force passed to applySteeringForce.
        //
        // allows a specific vehicle class to redefine this adjustment.
        // default is to disallow backward-facing steering at low speed.
        //
        // xxx should the default be this ad-hocery, or no adjustment?
        // xxx experimental 8-20-02
        //
        // parameter names commented out to prevent compiler warning from "-W"


        public Vector3 adjustRawSteeringForce(Vector3 force)//, const float /* deltaTime */)
        {
            float maxAdjustedSpeed = 0.2f * MaxSpeed;

            if ((Speed > maxAdjustedSpeed) || (force == Vector3.zero))
            {
                return force;
            }
            else
            {
                float range = Speed / maxAdjustedSpeed;
                float cosine = Mathf.Lerp(1.0f, -1.0f, Mathf.Pow(range, 20));
                Vector3 angle = OpenSteerUtility.limitMaxDeviationAngle(force, cosine, Forward);
                #if DEBUG
                // Debug.Log("Steer "+cosine+" "+angle+" "+range+" "+Mathf.Pow(range, 20));
                #endif
                return angle;
            }
        }


        // ----------------------------------------------------------------------------
        // xxx experimental 9-6-02
        //
        // apply a given braking force (for a given dt) to our momentum.
        //
        // (this is intended as a companion to applySteeringForce, but I'm not sure how
        // well integrated it is.  It was motivated by the fact that "braking" (as in
        // "capture the flag" endgame) by using "forward * speed * -rate" as a steering
        // force was causing problems in adjustRawSteeringForce.  In fact it made it
        // get NAN, but even if it had worked it would have defeated the braking.
        //
        // maybe the guts of applySteeringForce should be split off into a subroutine
        // used by both applySteeringForce and applyBrakingForce?


        void applyBrakingForce (float rate, float deltaTime)
        {
            float rawBraking = Speed * rate;
            float clipBraking = ((rawBraking < MaxForce) ?
                                       rawBraking :
                                       MaxForce);

            Speed = Speed - (clipBraking * deltaTime);
        }


        // ----------------------------------------------------------------------------
        // apply a given steering force to our momentum,
        // adjusting our orientation to maintain velocity-alignment.


        public void applySteeringForce(Vector3 force, float elapsedTime)
        {

            Vector3 adjustedForce = adjustRawSteeringForce (force);//, elapsedTime);
            
            // enforce limit on magnitude of steering force
            Vector3 clippedForce = truncateLength(adjustedForce, MaxForce);

            // compute acceleration and velocity
            Vector3 newAcceleration = (clippedForce / Mass);
            Vector3 newVelocity = Velocity;
            
            // damp out abrupt changes and oscillations in steering acceleration
            // (rate is proportional to time step, then clipped into useful range)
            if (elapsedTime > 0)
            {
                float smoothRate = Mathf.Clamp(9 * elapsedTime, 0.15f, 0.4f);
                _smoothedAcceleration=OpenSteerUtility.blendIntoAccumulator(smoothRate,
                                      newAcceleration,
                                      _smoothedAcceleration);
            }

            // Euler integrate (per frame) acceleration into velocity
            newVelocity += _smoothedAcceleration * elapsedTime;

            // enforce speed limit
            
            //newVelocity = newVelocity.truncateLength (MaxSpeed);
            newVelocity = truncateLength(newVelocity, MaxSpeed);
            
            // update Speed
            Speed = newVelocity.magnitude;

            // Euler integrate (per frame) velocity into position
            Position += (newVelocity * elapsedTime);
            
            // regenerate local space (by default: align vehicle's forward axis with
            // new velocity, but this behavior may be overridden by derived classes.)

            regenerateLocalSpace (newVelocity);//, elapsedTime);

            // maintain path curvature information
            measurePathCurvature (elapsedTime);

            // running average of recent positions
            _smoothedPosition=OpenSteerUtility.blendIntoAccumulator(elapsedTime * 0.06f, // QQQ
                                  Position ,
                                  _smoothedPosition);
        }


        // ----------------------------------------------------------------------------
        // the default version: keep FORWARD parallel to velocity, change UP as
        // little as possible.
        //
        // parameter names commented out to prevent compiler warning from "-W"

        void regenerateLocalSpace (Vector3 newVelocity)
        {
            // adjust orthonormal basis vectors to be aligned with new velocity
            if (Speed > 0) 
                Forward = newVelocity / Speed;
        }


        // ----------------------------------------------------------------------------
        // alternate version: keep FORWARD parallel to velocity, adjust UP according
        // to a no-basis-in-reality "banking" behavior, something like what birds and
        // airplanes do

        // XXX experimental cwr 6-5-03

        public void regenerateLocalSpaceForBanking (Vector3 newVelocity, float elapsedTime)
        {
            // the length of this global-upward-pointing vector controls the vehicle's
            // tendency to right itself as it is rolled over from turning acceleration
            Vector3 globalUp =new Vector3(0, 0.2f, 0);

            // acceleration points toward the center of local path curvature, the
            // length determines how much the vehicle will roll while turning
            Vector3 accelUp = _smoothedAcceleration * 0.05f;

            // combined banking, sum of UP due to turning and global UP
            Vector3 bankUp = accelUp + globalUp;

            // blend bankUp into vehicle's UP basis vector
            float smoothRate = elapsedTime * 3;
            Vector3 tempUp = Up;
            tempUp=OpenSteerUtility.blendIntoAccumulator(smoothRate, bankUp, tempUp);
            tempUp.Normalize();
//            setUp (tempUp);
			Up = tempUp;

        //  annotationLine (position(), position() + (globalUp * 4), gWhite);  // XXX
        //  annotationLine (position(), position() + (bankUp   * 4), gOrange); // XXX
        //  annotationLine (position(), position() + (accelUp  * 4), gRed);    // XXX
        //  annotationLine (position(), position() + (up ()    * 1), gYellow); // XXX

            // adjust orthonormal basis vectors to be aligned with new velocity
            if (Speed > 0) 
                Forward = (newVelocity / Speed);
        }


        // ----------------------------------------------------------------------------
        // measure path curvature (1/turning-radius), maintain smoothed version


        void measurePathCurvature (float elapsedTime)
        {
            if (elapsedTime > 0)
            {
                Vector3 dP = _lastPosition - Position;
                Vector3 dF = (_lastForward - Forward) / dP.magnitude;
                //SI - BIT OF A WEIRD FIX HERE . NOT SURE IF ITS CORRECT
                //Vector3 lateral = dF.perpendicularComponent (forward ());
                Vector3 lateral = OpenSteerUtility.perpendicularComponent( dF,Forward);

                float sign = (Vector3.Dot(lateral, Side) < 0) ? 1.0f : -1.0f;
                _curvature = lateral.magnitude * sign;
                //OpenSteerUtility.blendIntoAccumulator(elapsedTime * 4.0f, _curvature,_smoothedCurvature);
                _smoothedCurvature=OpenSteerUtility.blendIntoAccumulator(elapsedTime * 4.0f, _curvature, _smoothedCurvature);

                _lastForward = Forward;
                _lastPosition = Position;
            }
        }


        // ----------------------------------------------------------------------------
        // draw lines from vehicle's position showing its velocity and acceleration


        /*void annotationVelocityAcceleration (float maxLengthA,  float maxLengthV)
        {
            float desat = 0.4f;
            float aScale = maxLengthA / MaxForce;
            float vScale = maxLengthV / MaxSpeed;
            Vector3 p = Position;
            Vector3 aColor = new Vector3(desat, desat, 1); // bluish
            Vector3 vColor = new Vector3 (1, desat, 1); // pinkish

            //annotationLine (p, p + (velocity ()           * vScale), vColor);
            //annotationLine (p, p + (_smoothedAcceleration * aScale), aColor);
        }*/


        // ----------------------------------------------------------------------------
        // predict position of this vehicle at some time in the future
        // (assumes velocity remains constant, hence path is a straight line)
        //
        // XXX Want to encapsulate this since eventually I want to investigate
        // XXX non-linear predictors.  Maybe predictFutureLocalSpace ?
        //
        // XXX move to a vehicle utility mixin?


        public override Vector3 predictFuturePosition(float predictionTime)
        {
            return Position + (Velocity * predictionTime);
        }


        // ----------------------------------------------------------------------------
    }
}

