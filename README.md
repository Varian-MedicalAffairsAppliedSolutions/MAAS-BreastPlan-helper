# MAAS-BreastPlan-helper
<br>
Please find the latest precompiled binary version, ready to be used in Eclipse 15.6 and above in the releases section (on the right).

## Auto 3D sliding window tab
![image](https://github.com/Varian-MedicalAffairsAppliedSolutions/MAAS-BreastPlan-helper/assets/78000769/f537bdb0-d666-4242-babf-6839fbd1df2b)

This tab expects as a starting condition two open tangent fields with dose calculated (likely 6X energy)
After viewing the field separation and ensuring the other auto-populated fields are correct (user will likely need to correct the LMC version on the first run of the tool) the user can decide to try an auto plan with a specified goal max dose.  The plan will be created on either an automatically created PTV from an open field isodose line or from a user specified PTV.
The system will by default create an optimal fluence in 2 phases, additional options can be found in the config.json
Skin flash will need to be manually added (for now).
<br>
<br>

## Multi Field Beam Placement tab
![image](https://github.com/Varian-MedicalAffairsAppliedSolutions/MAAS-BreastPlan-helper/assets/78000769/69df09a9-b0df-4df4-8a70-42ca479082d5)
[Rayn K, Clark R, Hoxha K, et al. An IMRT planning technique for treating whole breast or chest wall with regional lymph nodes on Halcyon and Ethos. J Appl Clin Med Phys. 2024;e14295.](https://doi.org/10.1002/acm2.14295)
<br>
This tab expects as a starting condition one open field medial tangent field.  Then the user can update beam angles in multiples of 8 degrees from the initial seed beam to automate the tedious creation of so many fields.  This type of multifield arrangement is often needed when a user wants a VMAT-like dose distribution but desires full control of adding skin flash to tangent oriented fields only. 
<br>
<br>
## Tangent Auto Plan
[depreciated to be removed]
<br>
<br>
## Auto Fluence Extension
[coming soon]

Original 3D sliding window code courtesy of [LRCP Software](https://github.com/cancerhackr)

This project is currently a Work-In-Progress and it is not recommended to download any release yet, unless interested in helping with development.
