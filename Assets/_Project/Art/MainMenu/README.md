# Main-menu prototype art

The eleven owner-supplied cinematic images replace the previous four-image concept set. Every PNG is copied byte for byte at 3840 × 2160, in the slideshow order below. No generated variants or image edits are used. These are menu concept backdrops, not captures or registrations of playable Unity venues.

| Slideshow asset in Fields/ | Supplied source filename |
|---|---|
| Alley.png | Blacktop_Alley_Composite_3840x2160.png |
| Beach.png | Blacktop_Beach_Two_Cones_3840x2160.png |
| Downtown.png | Blacktop_Downtown_Draft2_Final_3840x2160.png |
| Junkyard.png | Blacktop_Junkyard_Final_3840x2160.png |
| Quad.png | Blacktop_Quad_Approved_3840x2160.png |
| Rooftop.png | Blacktop_Rooftop_Option_A_3840x2160.png |
| Shipyard.png | Blacktop_Shipyard_Amber_3840x2160.png |
| Suburbs.png | Blacktop_Suburbs_Composite_3840x2160.png |
| The-Lot.png | Blacktop_The_Lot_Composite_3840x2160.png |
| Underpass.png | Blacktop_Underpass_No_Lines_3840x2160.png |
| Warehouse.png | Blacktop_Warehouse_Composite_3840x2160.png |

MenuPrototypeSetup wires this ordered catalog explicitly on FrontEndSettings.asset. Imports retain non-power-of-two dimensions, allow up to 4096 pixels, disable mipmaps and use high-quality texture compression. The existing 6-second hold, 2.4-second crossfade, cover crop and submenu blur remain unchanged.

Fonts/BlacktopProDuo.ttf is the owner's supplied font. Fonts/BarlowCondensed-SemiBold.ttf comes from [Google Fonts](https://github.com/google/fonts/blob/main/ofl/barlowcondensed/BarlowCondensed-SemiBold.ttf); its OFL license is included in Fonts/OFL.txt. The setup utility creates and prewarms the accompanying TMP SDF assets. It does not change the scorebug's font theme.
