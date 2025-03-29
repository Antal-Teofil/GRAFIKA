using System;

using Silk.NET.Maths;

namespace Szeminarium
{
    internal class CameraDescriptor
    {
        public double DistanceToOrigin { get; private set; } = 1;

        public double AngleToZYPlane { get; private set; } = 0;

        public double AngleToZXPlane { get; private set; } = 0;

        const double DistanceScaleFactor = 1.1;

        const double AngleChangeStepSize = Math.PI / 180 * 5;

        /// <summary>
        /// Gets the position of the camera.
        /// </summary>
        public Vector3D<float> Position
        {
            get
            {
                return target + GetPointFromAngles(DistanceToOrigin, AngleToZYPlane, AngleToZXPlane);
            }
        }

        /// <summary>
        /// Gets the up vector of the camera.
        /// </summary>
        public Vector3D<float> UpVector
        {
            get
            {
                return Vector3D.Normalize(GetPointFromAngles(DistanceToOrigin, AngleToZYPlane, AngleToZXPlane + Math.PI / 2));
            }
        }

        /// <summary>
        /// Gets the target point of the camera view.
        /// </summary>
        private Vector3D<float> target = Vector3D<float>.Zero;
        public Vector3D<float> Target => target; // mostmar nem csak az origo korul lehet mozogni (=> expression-bodied meber), mintha get target lenne
        public void IncreaseZXAngle()
        {
            AngleToZXPlane += AngleChangeStepSize;
        }

        public void DecreaseZXAngle()
        {
            AngleToZXPlane -= AngleChangeStepSize;
        }

        public void IncreaseZYAngle()
        {
            AngleToZYPlane += AngleChangeStepSize;

        }

        public void DecreaseZYAngle()
        {
            AngleToZYPlane -= AngleChangeStepSize;
        }

        public void IncreaseDistance()
        {
            DistanceToOrigin = DistanceToOrigin * DistanceScaleFactor;
        }

        public void DecreaseDistance()
        {
            DistanceToOrigin = DistanceToOrigin / DistanceScaleFactor;
        }

        private static Vector3D<float> GetPointFromAngles(double distanceToOrigin, double angleToMinZYPlane, double angleToMinZXPlane)
        {
            var x = distanceToOrigin * Math.Cos(angleToMinZXPlane) * Math.Sin(angleToMinZYPlane);
            var z = distanceToOrigin * Math.Cos(angleToMinZXPlane) * Math.Cos(angleToMinZYPlane);
            var y = distanceToOrigin * Math.Sin(angleToMinZXPlane);

            return new Vector3D<float>((float)x, (float)y, (float)z);
        }

        public void moveForward(float amount)
        {
            var forward = Vector3D.Normalize(target - Position);
            target += forward * amount;
        }

        public void moveRight(float amount)
        {
            var forward = Vector3D.Normalize(target - Position);
            var right = Vector3D.Normalize(Vector3D.Cross(forward, UpVector));
            target += right * amount;
        }
    }
}
