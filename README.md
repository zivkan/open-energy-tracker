# open-energy-tracker

The Australian Energy Regulator (AER) has a [page on the Energy Product Reference Data](https://www.aer.gov.au/energy-product-reference-data), with details on how to use the API to get energy plan data.

However, their FAQ says:

> Does the Get Generic Plans API only show current plans?
>
> The AER provides data from the Get Generic Plans endpoint, which includes access to current plans. Future and historical plans cannot be accessed.

This repo attempts to start capturing all plan data so that in the future energy prices can be compared over time.
Inspired by https://github.com/LukePrior/open-energy-tracker.

## Data format

This repo stores plan details exactly as they're returned from the CDR API.
Currently, all plan details are using their [v3 format](https://consumerdatastandardsaustralia.github.io/standards/index.html#get-generic-plan-detail).
