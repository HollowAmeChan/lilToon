#ifndef LIL_HO_SURFACE_ALPHA_INCLUDED
#define LIL_HO_SURFACE_ALPHA_INCLUDED

// Infrastructure must not leave depth/identity/surface records for an
// invisible ordinary transparent fragment. Match Forward's cutoff, and also
// reject exactly zero alpha when cutoff is zero. Opaque alpha is not opacity;
// refraction can remain visible at zero colour alpha, so neither is discarded.
void lilHoClipTransparentInfrastructure(float alpha)
{
    #if LIL_RENDER == 2 && !defined(LIL_REFRACTION)
        clip(alpha - _Cutoff);
        clip(alpha > 0.0 ? 1.0 : -1.0);
    #endif
}

#endif
