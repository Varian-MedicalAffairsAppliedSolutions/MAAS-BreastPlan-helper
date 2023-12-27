## MAAS-BreastPlan-helper
![image](https://github.com/Varian-MedicalAffairsAppliedSolutions/MAAS-BreastPlan-helper/assets/78000769/f537bdb0-d666-4242-babf-6839fbd1df2b)
# Auto 3D sliding window tab
This tab expects as a starting condition two open tangent fields with dose calculated (likely 6X energy)
After viewing the field separation and ensuring the other auto-populated fields are correct (user will likely need to correct the LMC version on the first run of the tool) the user can decide to try an auto plan with a specified goal max dose.  The plan will be created on either an automatically created PTV from an open field isodose line or from a user specified PTV.
The system will by default create an optimal fluence in 2 phases, additional options can be found in the config.json
Skin flash will need to be manually added (for now).
<br>
![image](https://github.com/Varian-MedicalAffairsAppliedSolutions/MAAS-BreastPlan-helper/assets/78000769/69df09a9-b0df-4df4-8a70-42ca479082d5)
# Multi Field Beam Placement
This tab expects as a starting condition one open field medial tangent field.  Then the user can update beam angles in multiples of 8 degrees from the initial seed beam to automate the tedious creation of so many fields.  This type of multifield arrangement is often needed when a user wants a VMAT-like dose distribution but desires full control of adding skin flash to tangent oriented fields only. 
<br>
# Tangent Auto Plan
[depreciated to be removed]
<br>
# Auto Fluence Extension
[coming soon]

Original 3D sliding window code courtesy of [LRCP Software](https://github.com/cancerhackr)

This project is currently a Work-In-Progress and it is not recommended to download any release yet, unless interested in helping with development.
