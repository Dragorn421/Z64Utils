﻿#version 330 core

in vec2 v_VtxTexCoords;
in vec4 v_VtxColor;

uniform vec3 u_DirLight;
uniform sampler2D u_Tex;
uniform vec4 u_PrimColor;
uniform vec4 u_HighlightColor;
uniform bool u_TexEnabled;

void main()
{
	/* texture */
	gl_FragColor = u_TexEnabled
		? texture(u_Tex, v_VtxTexCoords)
		: vec4(0, 0, 0, 1);
	
	/* blending */
	// FragColor *= v_VtxColor;

	/* highlight */
	gl_FragColor = mix(gl_FragColor, u_HighlightColor, vec4(u_HighlightColor.a));
	//gl_FragColor = vec4(1, 0, 0, 1);
}