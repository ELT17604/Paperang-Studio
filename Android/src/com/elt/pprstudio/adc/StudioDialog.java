package com.elt.pprstudio.adc;

import android.animation.ValueAnimator;
import android.app.Dialog;
import android.content.DialogInterface;
import android.view.Gravity;
import android.view.View;
import android.view.WindowManager;
import android.view.animation.PathInterpolator;
import android.widget.LinearLayout;
import android.widget.ScrollView;

final class StudioDialog
{
	private final MainActivity app;
	private CharSequence title, message, positive, negative;
	private DialogInterface.OnClickListener positiveAction, negativeAction, itemAction;
	private CharSequence[] items;
	private View content;
	private boolean keepOpen;

	StudioDialog(MainActivity app)
	{
		this.app = app;
	}

	StudioDialog setTitle(CharSequence value)
	{
		title = value;
		return this;
	}
	StudioDialog setMessage(CharSequence value)
	{
		message = value;
		return this;
	}
	StudioDialog setView(View value)
	{
		content = value;
		return this;
	}

	StudioDialog setItems(CharSequence[] values, DialogInterface.OnClickListener action)
	{
		items = values;
		itemAction = action;
		return this;
	}

	StudioDialog setPositiveButton(CharSequence label, DialogInterface.OnClickListener action)
	{
		positive = label;
		positiveAction = action;
		return this;
	}

	StudioDialog setNegativeButton(CharSequence label, DialogInterface.OnClickListener action)
	{
		negative = label;
		negativeAction = action;
		return this;
	}

	StudioDialog keepOpenOnApply()
	{
		keepOpen = true;
		return this;
	}

	Dialog show()
	{
		Dialog dialog = new Dialog(app);
		dialog.requestWindowFeature(android.view.Window.FEATURE_NO_TITLE);
		var panel = app.column();
		panel.setFocusableInTouchMode(true);
		panel.setPadding(app.dp(20), app.dp(16), app.dp(20), app.dp(16));
		if (title != null)
		{
			var heading = app.label(title.toString(), 23);
			heading.setTypeface(android.graphics.Typeface.create("sans-serif-medium", 0));
			heading.setAccessibilityHeading(true);
			panel.addView(heading);
		}
		if (message != null)
			panel.addView(app.label(message.toString(), 15));
		if (content != null)
			panel.addView(content, new LinearLayout.LayoutParams(-1, -2, 1));
		if (items != null)
		{
			var list = app.column();
			for (int i = 0; i < items.length; i++)
			{
				int index = i;
				var button = app.button(items[i].toString(), () -> {
					dialog.dismiss();
					itemAction.onClick(dialog, index);
				});
				button.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
				button.setPadding(app.dp(14), app.dp(8), app.dp(14), app.dp(8));
				list.addView(button);
			}
			var scroll = new ScrollView(app);
			scroll.addView(list);
			panel.addView(scroll, new LinearLayout.LayoutParams(-1, -2, 1));
		}
		if (positive != null || negative != null)
		{
			var footer = new LinearLayout(app);
			footer.setGravity(Gravity.END);
			footer.setPadding(0, app.dp(8), 0, 0);
			addAction(footer, dialog, negative, negativeAction, DialogInterface.BUTTON_NEGATIVE, false);
			addAction(footer, dialog, positive, positiveAction, DialogInterface.BUTTON_POSITIVE, keepOpen);
			panel.addView(footer);
		}
		dialog.setContentView(panel);
		var window = dialog.getWindow();
		window.setBackgroundDrawable(app.shape(app.getColor(android.R.color.system_neutral1_50), 28));
		window.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND | WindowManager.LayoutParams.FLAG_BLUR_BEHIND);
		var attributes = window.getAttributes();
		attributes.setBlurBehindRadius(app.dp(12));
		window.setAttributes(attributes);
		window.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE |
		                        WindowManager.LayoutParams.SOFT_INPUT_STATE_ALWAYS_HIDDEN);
		dialog.show();
		int width = app.getResources().getDisplayMetrics().widthPixels;
		window.setLayout(Math.min(width - app.dp(32), app.dp(460)), -2);
		panel.requestFocus();
		var manager = app.getSystemService(WindowManager.class);
		java.util.function.Consumer<Boolean> blur = enabled -> window.setDimAmount(enabled ? .28f : .44f);
		blur.accept(manager.isCrossWindowBlurEnabled());
		manager.addCrossWindowBlurEnabledListener(app.getMainExecutor(), blur);
		dialog.setOnDismissListener(ignored -> manager.removeCrossWindowBlurEnabledListener(blur));
		if (ValueAnimator.areAnimatorsEnabled())
		{
			panel.setAlpha(0);
			panel.setTranslationY(app.dp(8));
			panel.animate()
			    .alpha(1)
			    .translationY(0)
			    .setDuration(180)
			    .setInterpolator(new PathInterpolator(.2f, 0, 0, 1))
			    .start();
		}
		return dialog;
	}

	private void addAction(LinearLayout footer, Dialog dialog, CharSequence label,
	                       DialogInterface.OnClickListener action, int which, boolean stay)
	{
		if (label == null)
			return;
		var button = app.button(label.toString(), () -> {
			if (!stay)
				dialog.dismiss();
			if (action != null)
				action.onClick(dialog, which);
		});
		app.setSelectedStyle(button, which == DialogInterface.BUTTON_POSITIVE);
		button.setPadding(app.dp(16), app.dp(8), app.dp(16), app.dp(8));
		var params = new LinearLayout.LayoutParams(-2, -2);
		params.setMarginStart(app.dp(6));
		footer.addView(button, params);
	}
}
