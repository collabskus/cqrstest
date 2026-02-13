  __  __           _   _     _   ____    _       ____
 |  \/  |  _   _  | | | |_  (_) |  _ \  | |__   / ___|   _   _   _ __     ___
 | |\/| | | | | | | | | __| | | | | | | | '_ \  \___ \  | | | | | '_ \   / __|
 | |  | | | |_| | | | | |_  | | | |_| | | |_) |  ___) | | |_| | | | | | | (__
 |_|  |_|  \__,_| |_|  \__| |_| |____/  |_.__/  |____/   \__, | |_| |_|  \___|
                                                         |___/

/ Creating node1...
                   EF ENTITY: MultiDbSync.Domain.Entities.Customer
EF ENTITY: MultiDbSync.Domain.Entities.DatabaseNode
EF ENTITY: MultiDbSync.Domain.Entities.Order
EF ENTITY: MultiDbSync.Domain.Entities.OrderItem
√ Database nodes initialized successfully!

Automated CI/CD Demo - High Volume Data Operations


Creating 10000 products ---------------------------------------- 100% 00:00:00

√ Created 10000 products

┌─Database Statistics───────────────────────────┐
│ ┌───────────────────────┬───────────────────┐ │
│ │        Metric         │       Value       │ │
│ ├───────────────────────┼───────────────────┤ │
│ │    Total Products     │       10000       │ │
│ │   Total Stock Units   │     2,491,867     │ │
│ │       Avg Price       │     $1,006.06     │ │
│ │ Total Inventory Value │ $2,507,703,798.74 │ │
│ │      Categories       │         5         │ │
│ └───────────────────────┴───────────────────┘ │
└───────────────────────────────────────────────┘

Products by Category:
┌─────────────┬───────┬─────────────────┬───────────┐
│ Category    │ Count │ Total Value     │ Avg Stock │
├─────────────┼───────┼─────────────────┼───────────┤
│ Electronics │ 2039  │ $516,811,566.49 │ 247       │
│ Peripherals │ 2017  │ $517,486,749.00 │ 256       │
│ Software    │ 2006  │ $502,771,823.60 │ 247       │
│ Accessories │ 2002  │ $490,548,192.32 │ 249       │
│ Components  │ 1936  │ $480,085,467.33 │ 247       │
└─────────────┴───────┴─────────────────┴───────────┘

Phase 3: Performing bulk stock updates...

Updating stock levels ---------------------------------------- 100%

√ Updated 50 product stock levels

Phase 4: Adjusting prices...

Applying price changes ---------------------------------------- 100%

√ Updated 30 product prices

Phase 5: Removing discontinued products...
√ Removed 5 discontinued products

┌─Before & After Comparison───────────────────────────┐
│ ┌────────────────┬───────────┬───────────┬────────┐ │
│ │ Metric         │ Before    │ After     │ Change │ │
│ ├────────────────┼───────────┼───────────┼────────┤ │
│ │ Total Products │ 10000     │ 9995      │ -5     │ │
│ │ Total Stock    │ 2,484,904 │ 2,484,904 │ +0     │ │
│ └────────────────┴───────────┴───────────┴────────┘ │
└─────────────────────────────────────────────────────┘

Sample Products (Top 10 by Value):
┌──────────────────────────┬─────────────┬───────────┬───────┬─────────────┐
│ Name                     │ Category    │ Price     │ Stock │ Value       │
├──────────────────────────┼─────────────┼───────────┼───────┼─────────────┤
│ Professional Laptop 8635 │ Peripherals │ $2,008.55 │ 493   │ $990,215.15 │
│ Gaming Keyboard 2777     │ Components  │ $1,996.38 │ 492   │ $982,218.96 │
│ Compact Cable 3209       │ Electronics │ $1,970.36 │ 498   │ $981,239.28 │
│ Budget Monitor 479       │ Electronics │ $1,969.52 │ 495   │ $974,912.40 │
│ Premium Mouse 7673       │ Accessories │ $2,003.79 │ 486   │ $973,841.94 │
│ Budget Cable 4339        │ Components  │ $1,999.67 │ 487   │ $973,839.29 │
│ Gaming Monitor 7968      │ Peripherals │ $1,954.18 │ 497   │ $971,227.46 │
│ Compact Keyboard 8703    │ Accessories │ $1,965.07 │ 492   │ $966,814.44 │
│ Gaming Microphone 1434   │ Accessories │ $1,980.39 │ 488   │ $966,430.32 │
│ Premium Microphone 9985  │ Software    │ $2,008.26 │ 479   │ $961,956.54 │
└──────────────────────────┴─────────────┴───────────┴───────┴─────────────┘

√ Automated demo completed successfully!
All operations logged and synchronized across nodes.

D:\DEV\personal\cqrstest\MultiDbSync\MultiDbSync.Console\bin\Debug\net10.0\MultiDbSync.Console.exe (process 10576) exited with code 0 (0x0).
To automatically close the console when debugging stops, enable Tools->Options->Debugging->Automatically close the console when debugging stops.
Press any key to close this window . . .

