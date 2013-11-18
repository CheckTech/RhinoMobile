//
// ViewportInfoExtensions.cs
// RhinoMobile.Display
//
// Created by dan (dan@mcneel.com) on 10/10/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using System;
using System.Drawing;

using Rhino.DocObjects;

namespace RhinoMobile.Display
{
	public static class ViewportInfoExtensions
	{
		/// <summary>
		/// LateralPan of a viewport between two points
		/// </summary>
		public static void LateralPan (this ViewportInfo viewport, System.Drawing.PointF fromPoint, System.Drawing.PointF toPoint)
		{
			double deltaX, deltaY, s;
			Rhino.Geometry.Transform s2c = viewport.GetXform (CoordinateSystem.Screen, CoordinateSystem.Clip);
			Rhino.Geometry.Point3d screenPoint0 = new Rhino.Geometry.Point3d (fromPoint.X, fromPoint.Y, 0.0);
			Rhino.Geometry.Point3d screenPoint1 = new Rhino.Geometry.Point3d (toPoint.X, toPoint.Y, 0.0);
			Rhino.Geometry.Point3d clipPoint0 = s2c * screenPoint0;
			Rhino.Geometry.Point3d clipPoint1 = s2c * screenPoint1;
			deltaX = 0.5 * (clipPoint1.X - clipPoint0.X);
			deltaY = 0.5 * (clipPoint1.Y - clipPoint0.Y);
			deltaX *= (viewport.FrustumRight - viewport.FrustumLeft);
			deltaY *= (viewport.FrustumTop - viewport.FrustumBottom);
			if (viewport.IsPerspectiveProjection) {
				s = viewport.TargetPoint.DistanceTo (viewport.CameraLocation) / viewport.FrustumNear;
				deltaX *= s;
				deltaY *= s;
			}
			Rhino.Geometry.Vector3d dollyVector = (deltaX * viewport.CameraX) + (deltaY * viewport.CameraY);
			viewport.TargetPoint = viewport.TargetPoint - dollyVector;
			viewport.SetCameraLocation (viewport.CameraLocation - dollyVector);
		}

		/// <summary>
		/// <para>Magnify/Zoom the Camera in a viewport</para>
		/// <para>method =</para>
		/// <para>0 performs a "dolly" magnification by moving the 
		///   camera along the camera direction vector so that
		///   the amount of the screen subtended by an object
		///   changes.</para>
		/// <para>1 performs a "zoom" magnification by adjusting the
		///   "lens" angle</para>
		/// </summary>
		public static bool Magnify (this ViewportInfo viewport, Size viewSize, double magnifcationFactor, int method, System.Drawing.PointF fixedScreenPoint)
		{
			if (viewport.IsCameraLocationLocked)
				return false;

			int screenWidth = viewSize.Width;
			int screenHeight = viewSize.Height;

			if (1 > screenWidth || 1 > screenHeight)
				return false;

			// move camera toward target to magnify
			if (magnifcationFactor > 0) {
				// if the screen point is not in the viewport, then ignore it.
				if (!fixedScreenPoint.IsEmpty) {
					if (fixedScreenPoint.X <= 0 || fixedScreenPoint.X >= screenWidth - 1 || fixedScreenPoint.Y <= 0 || fixedScreenPoint.Y >= screenHeight - 1) {
						fixedScreenPoint.X = 0;
						fixedScreenPoint.Y = 0;
					}
				}
				double frustumLeft = viewport.FrustumLeft;
				double frustumRight = viewport.FrustumRight;
				double frustumBottom = viewport.FrustumBottom;
				double frustumTop = viewport.FrustumTop;
				double frustumNear = viewport.FrustumNear;
				double frustumFar = viewport.FrustumFar;
				double frustumWidth = viewport.FrustumWidth;
				double frustumHeight = viewport.FrustumHeight;
				double d = 0.0;

				// dolly camera towards target point...
				if (viewport.IsPerspectiveProjection && method == 0) {
					const double miniumumTargetDistance = 0.000001;
					var cameraZ = viewport.CameraZ;
					var cameraLocation = viewport.CameraLocation;
					var target = viewport.TargetPoint;
					double targetDistance = (cameraLocation - target) * cameraZ;
					if (targetDistance >= 0.0) {
						double delta = (1.0 - (1.0/magnifcationFactor)) * targetDistance;
						if (targetDistance - delta > miniumumTargetDistance) {
							cameraLocation = cameraLocation - (delta * cameraZ);
							viewport.SetCameraLocation (cameraLocation);
							if (!fixedScreenPoint.IsEmpty) {
								d = targetDistance / viewport.FrustumNear;
								frustumWidth *= d;
								frustumHeight *= d;
								d = (targetDistance - delta) / targetDistance;
							}
						}
					}
				}
				if (method == 1) {
					// parallel proj or "true" zoom
					// apply magnification to frustum
					d = 1.0 / magnifcationFactor;
					frustumLeft *= d;
					frustumRight *= d;
					frustumBottom *= d;
					frustumTop *= d;
					viewport.SetFrustum (frustumLeft, frustumRight, frustumBottom, frustumTop, frustumNear, frustumFar);
				}

				if (!fixedScreenPoint.IsEmpty && d != Double.Epsilon) {
					// lateral dolly to keep fixed_screen_point in same location on screen
					Rhino.Geometry.Vector3d scale = new Rhino.Geometry.Vector3d (1.0, 1.0, 1.0);
					scale.X = viewport.ViewScale.Width;
					scale.Y = viewport.ViewScale.Height;
					double fx = ((double)fixedScreenPoint.X / (double)screenWidth);
					double fy = ((double)fixedScreenPoint.Y / (double)screenHeight);
					double dx = ((0.5 - fx) * (1.0 - d) * frustumWidth) / scale.X;
					double dy = ((fy - 0.5) * (1.0 - d) * frustumHeight) / scale.Y;
					Rhino.Geometry.Vector3d dollyVector = dx * viewport.CameraX + dy * viewport.CameraY;
					var cameraLocation = viewport.CameraLocation;
					var target = viewport.TargetPoint;
					viewport.TargetPoint = target - dollyVector;
					viewport.SetCameraLocation (cameraLocation - dollyVector);
				}

			}
			return true;
		}

		/// <summary>
		/// GestoreOrbit performs a gesture-based orbit about an anchorLocation to a location.
		/// </summary>
		public static void GestureOrbit (this ViewportInfo viewport, Size viewSize, System.Drawing.PointF anchorLocation, System.Drawing.PointF location)
		{
			int screenWidth = viewSize.Width;
			float f = (float)(Math.PI / screenWidth);
			float horizontalDiff = anchorLocation.X - location.X;
			RotateLeftRight (viewport, (horizontalDiff * f));
			float verticalDiff = anchorLocation.Y - location.Y;
			RotateUpDown (viewport, (verticalDiff * f));
		}

		/// <summary>
		/// Rotates the viewport around an axis by an angle
		/// </summary>
		public static void RotateCameraAround (this ViewportInfo viewport, Rhino.Geometry.Vector3d axis, double angle)
		{
			Rhino.Geometry.Point3d cameraLocation = viewport.CameraLocation;
			double targetDistance = (cameraLocation - viewport.TargetPoint) * viewport.CameraZ;
			Rhino.Geometry.Transform rotationTransform = Rhino.Geometry.Transform.Rotation (angle, axis, cameraLocation);
			Rhino.Geometry.Vector3d cameraDirection = -(rotationTransform * viewport.CameraZ);
			if (cameraDirection.Z >= -0.99 && cameraDirection.Z <= 0.99) {  	// avoid gimbal "lock"
				Rhino.Geometry.Point3d target = cameraLocation + targetDistance * cameraDirection;
				viewport.TargetPoint = target;
				viewport.SetCameraLocation (cameraLocation);
				viewport.SetCameraDirection (cameraDirection);
				viewport.SetCameraUp (Rhino.Geometry.Vector3d.ZAxis);
			}
		}

		/// <summary>
		/// Rotate the camera about world z axis
		/// </summary>
		public static void RotateLeftRight (this ViewportInfo viewport, double angle)
		{
			var axis = Rhino.Geometry.Vector3d.ZAxis;
			Rhino.Geometry.Point3d center = viewport.TargetPoint;
			RotateView (viewport, axis, center, angle);
		}

		/// <summary>
		/// Rotates camera around the x axis
		/// </summary>
		public static void RotateUpDown (this ViewportInfo viewport, double angle)
		{
			Rhino.Geometry.Vector3d cameraX;
			Rhino.Geometry.Point3d center;
			center = viewport.TargetPoint;
			cameraX = viewport.CameraX;
			RotateView (viewport, cameraX, center, angle);
		}

		/// <summary>
		/// Rotates a viewport about an axis relative to a center point
		/// </summary>
		public static void RotateView (this ViewportInfo viewport, Rhino.Geometry.Vector3d axis, Rhino.Geometry.Point3d center, double angle)
		{
			Rhino.Geometry.Point3d cameraLocation = viewport.CameraLocation;
			Rhino.Geometry.Vector3d cameraY = viewport.CameraY;
			Rhino.Geometry.Vector3d cameraZ = viewport.CameraZ;
			Rhino.Geometry.Transform rotation = Rhino.Geometry.Transform.Rotation (angle, axis, center);
			cameraLocation = rotation * cameraLocation;
			cameraY = rotation * cameraY;
			cameraZ = -(rotation * cameraZ);
			viewport.SetCameraLocation (cameraLocation);
			viewport.SetCameraDirection (cameraZ);
			viewport.SetCameraUp (cameraY);
		}

		/// <summary>
		/// Sets the viewport's camera location, target and up vector
		/// </summary>
		public static void SetTarget (this ViewportInfo viewport, Rhino.Geometry.Point3d targetLocation, Rhino.Geometry.Point3d cameraLocation, Rhino.Geometry.Vector3d cameraUp)
		{
			Rhino.Geometry.Vector3d cameraDirection = viewport.CameraDirection;
			cameraDirection.Unitize ();

			if (! viewport.CameraDirection.IsTiny ()) {
				Rhino.Geometry.Vector3d cameraDirection0 = -viewport.CameraZ;
				Rhino.Geometry.Vector3d cameraY = viewport.CameraY;
				const double tiltAngle = 0;

				viewport.SetCameraLocation (cameraLocation);
				viewport.SetCameraDirection (cameraDirection);

				bool rc = false;
				rc = viewport.SetCameraUp (cameraUp);

				if (!rc) {
					rc = viewport.SetCameraUp (cameraY);
					cameraUp = cameraY;
				}

				if (!rc) {
					Rhino.Geometry.Vector3d rotationAxis = Rhino.Geometry.Vector3d.CrossProduct (cameraDirection0, cameraDirection);
					double sinAngle = rotationAxis.Length;
					double cosAngle = cameraDirection0 * cameraDirection;
					Rhino.Geometry.Transform rot = Rhino.Geometry.Transform.Rotation (sinAngle, cosAngle, rotationAxis, Rhino.Geometry.Point3d.Origin);
					cameraUp = rot * cameraY;
					rc = viewport.SetCameraUp (cameraUp);
				}

				if (rc) {
					// Apply tilt angle to new camera and target location
					if (Math.Abs (tiltAngle) > 1.0e-6) {
						Rhino.Geometry.Transform rot = Rhino.Geometry.Transform.Rotation (tiltAngle, -cameraDirection0, cameraLocation);
						cameraUp = rot * cameraUp;
						rc = viewport.SetCameraUp (cameraUp);
					}

					if (rc)
						viewport.TargetPoint = targetLocation;
				}
			}
		}
	
	}
}